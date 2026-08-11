using System.Linq;
using System.Numerics;
using System.Threading;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Screens;

public sealed partial class OnyxTextRenderingOverlay : Overlay
{
    private const float FontScale = 1f;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IGameTiming _timing = default!;
    private SpriteSystem _sprite;
    private Queue<(Entity<OnyxTextVisualsComponent> Entity, Font Font, CancellationToken Cancellation)> _queue = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpaceBelowWorld;
    public OnyxTextRenderingOverlay(SpriteSystem sprite)
    {
        IoCManager.InjectDependencies(this);
        _sprite = sprite;
        ZIndex = -100;
    }

    public CancellationTokenSource QueueRender(Entity<OnyxTextVisualsComponent> ent, Font font)
    {
        var source = new CancellationTokenSource();
        _queue.Enqueue((ent, font, source.Token));
        return source;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var screenHandle = args.ScreenHandle;
        while (_queue.TryDequeue(out var queued))
        {
            if (queued.Cancellation.IsCancellationRequested)
                continue;

            foreach (var row in queued.Entity.Comp.Rows)
            {
                if (row.Text == string.Empty)
                {
                    _sprite.LayerSetTexture(queued.Entity.Owner, row.Layer, null);
                    row.Texture?.Dispose();
                    row.Texture = null;
                    row.Marquee = false;
                    continue;
                }

                var dimensions = screenHandle.GetDimensions(queued.Font, row.Text, FontScale);
                row.Marquee = dimensions.X > queued.Entity.Comp.MarqueeWidth;
                var size = new Vector2i(
                    row.Marquee ? queued.Entity.Comp.MarqueeWidth : (int) MathF.Round(dimensions.X),
                    (int) MathF.Round(dimensions.Y));
                if (row.Texture is null || row.Texture.Size != size)
                {
                    row.Texture?.Dispose();
                    row.Texture = _clyde.CreateRenderTarget(size,
                        new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
                        name: $"onyx-text-visuals-{queued.Entity.Owner.Id}");
                }

                _sprite.LayerSetTexture(queued.Entity.Owner, row.Layer, row.Texture.Texture);
                _sprite.LayerSetOffset(queued.Entity.Owner, row.Layer, row.Offset);

                args.DrawingHandle.RenderInRenderTarget(row.Texture, () =>
                {
                    var offset = Vector2.Zero;
                    if (row.Marquee)
                    {
                        var distance = dimensions.X + queued.Entity.Comp.MarqueeWidth + queued.Entity.Comp.MarqueePadding * 2;
                        var elapsed = _timing.CurTime.TotalSeconds;
                        offset.X = queued.Entity.Comp.MarqueeWidth + queued.Entity.Comp.MarqueePadding
                            - (float) ((elapsed / queued.Entity.Comp.MarqueeRate.TotalSeconds) % distance);
                    }

                    screenHandle.DrawString(queued.Font, offset, row.Text, FontScale, Color.White);
                }, Color.Transparent);
            }
        }
    }
}

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Screens;

public sealed partial class StatusDisplayTextVisualsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IResourceCache _resource = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private StatusDisplayTextRenderingOverlay _textRendering = default!;
    private Font _font = default!;

    public override void Initialize()
    {
        _textRendering = new StatusDisplayTextRenderingOverlay(_sprite);
        _overlay.AddOverlay(_textRendering);
        _font = new VectorFont(_resource.GetResource<FontResource>("/Fonts/_Onyx/Tiny5-Regular.ttf"), 6);

        SubscribeLocalEvent<StatusDisplayTextVisualsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<StatusDisplayTextVisualsComponent, ComponentShutdown>(OnComponentShutdown);
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay(_textRendering);
    }

    private void OnComponentInit(Entity<StatusDisplayTextVisualsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Token = _textRendering.QueueRender(ent, _font);
    }

    private void OnComponentShutdown(Entity<StatusDisplayTextVisualsComponent> ent, ref ComponentShutdown args)
    {
        foreach (var row in ent.Comp.Rows)
            row.Texture?.Dispose();

        ent.Comp.Token?.Cancel();
    }

    public void SetText(Entity<StatusDisplayTextVisualsComponent?> ent, bool force, params string[] rows)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var count = Math.Min(rows.Length, ent.Comp.Rows.Count);
        var changed = false;
        for (var i = 0; i < count; i++)
        {
            if (ent.Comp.Rows[i].Text == rows[i])
                continue;

            ent.Comp.Rows[i].Text = rows[i];
            changed = true;
        }

        if (!changed && !force)
            return;

        ent.Comp.Token?.Cancel();
        ent.Comp.Token = _textRendering.QueueRender((ent, ent.Comp), _font);
    }

    public void SetText(Entity<StatusDisplayTextVisualsComponent?> ent, params string[] rows)
    {
        SetText(ent, false, rows);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<StatusDisplayTextVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
        {
            if (!visuals.Rows.Exists(row => row.Marquee))
                continue;

            visuals.Token?.Cancel();
            visuals.Token = _textRendering.QueueRender((uid, visuals), _font);
        }
    }
}

using System.Numerics;
using System.Linq;
using Content.Shared.CrusherUpgrades;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CrusherUpgrades;

public sealed partial class TrailSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private TrailOverlay? _overlay;

    public override void Initialize()
    {
        _overlay = new TrailOverlay(EntityManager, _timing, _prototypes);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<TrailOverlay>();
        base.Shutdown();
    }
}

public sealed class TrailOverlay(IEntityManager entities, IGameTiming timing, IPrototypeManager prototypes) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly Dictionary<EntityUid, TrailRecord> _trails = new();
    private readonly TransformSystem _transform = entities.System<TransformSystem>();
    private readonly SpriteSystem _sprites = entities.System<SpriteSystem>();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var seen = new HashSet<EntityUid>();
        var query = entities.EntityQueryEnumerator<TrailComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform))
        {
            seen.Add(uid);
            if (!_trails.TryGetValue(uid, out var record))
                _trails[uid] = record = new TrailRecord();

            var now = timing.CurTime;
            if (now >= record.NextPoint)
            {
                record.NextPoint = now + TimeSpan.FromSeconds(trail.Frequency);
                record.Points.Add(new TrailPoint(_transform.GetWorldPosition(xform), now));
            }
            record.Points.RemoveAll(point => now - point.Time > TimeSpan.FromSeconds(trail.Lifetime));
            DrawTrail(args, trail, record.Points, now);
        }

        foreach (var uid in _trails.Keys.Where(uid => !seen.Contains(uid)).ToArray())
            _trails.Remove(uid);
    }

    private void DrawTrail(in OverlayDrawArgs args, TrailComponent trail, List<TrailPoint> points, TimeSpan now)
    {
        var handle = args.WorldHandle;
        if (trail.Shader != null && prototypes.TryIndex<ShaderPrototype>(trail.Shader, out var shader))
            handle.UseShader(shader.InstanceUnique());

        foreach (var point in points)
        {
            var steps = (float) ((now - point.Time).TotalSeconds / Math.Max(trail.LerpTime, 0.001));
            var alpha = MathF.Max(0, trail.Color.A - trail.AlphaLerpAmount * steps);
            var scale = MathF.Max(0, trail.Scale - trail.ScaleLerpAmount * steps);
            if (alpha <= 0 || scale <= 0 || !args.WorldAABB.Contains(point.Position))
                continue;

            var color = trail.Color.WithAlpha(alpha);
            if (trail.Sprite is { } sprite)
            {
                var texture = _sprites.Frame0(sprite);
                handle.SetTransform(Matrix3x2.CreateScale(scale) * Matrix3Helpers.CreateTranslation(point.Position));
                handle.DrawTexture(texture, -(Vector2) texture.Size / 2 / EyeManager.PixelsPerMeter, color);
            }
            else
                handle.DrawCircle(point.Position, scale * 0.5f, color);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private sealed class TrailRecord
    {
        public TimeSpan NextPoint;
        public readonly List<TrailPoint> Points = new();
    }

    private readonly record struct TrailPoint(Vector2 Position, TimeSpan Time);
}

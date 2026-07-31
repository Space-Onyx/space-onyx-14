using System.Numerics;
using Content.Shared._Onyx.FireControl;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Onyx.FireControl;

public sealed partial class FireControlVisualizerSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private readonly Dictionary<EntityUid, Dictionary<float, bool>> _visualizations = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FireControlVisualizationEvent>(OnVisualization);
        _overlayManager.AddOverlay(new FireControlOverlay(_visualizations, EntityManager));
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay<FireControlOverlay>();
        base.Shutdown();
    }

    private void OnVisualization(FireControlVisualizationEvent args)
    {
        var uid = GetEntity(args.Entity);
        if (args.Enabled && args.Directions != null)
            _visualizations[uid] = args.Directions;
        else
            _visualizations.Remove(uid);
    }

    private sealed class FireControlOverlay(
        IReadOnlyDictionary<EntityUid, Dictionary<float, bool>> visualizations,
        IEntityManager entityManager) : Overlay
    {
        private readonly SharedTransformSystem _transform = entityManager.System<SharedTransformSystem>();

        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        protected override void Draw(in OverlayDrawArgs args)
        {
            foreach (var (uid, directions) in visualizations)
            {
                if (!entityManager.TryGetComponent(uid, out TransformComponent? transform))
                    continue;

                var position = _transform.GetWorldPosition(transform);
                args.WorldHandle.DrawCircle(position, 0.3f, Color.Yellow, true);

                foreach (var (angle, canFire) in directions)
                {
                    var radians = angle * MathF.PI / 180f;
                    var direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
                    args.WorldHandle.DrawLine(position, position + direction * 25f, canFire ? Color.Green : Color.Red);
                }
            }
        }
    }
}

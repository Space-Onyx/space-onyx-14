using Content.Client.Eye;
using Content.Shared._Onyx.Body;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;

namespace Content.Client._Onyx.Body;

public sealed partial class DirectionalLimbLayerSystem : EntitySystem
{
    private const string LegOverlay = "directional-limb-leg-overlay";
    private const string FootOverlay = "directional-limb-foot-overlay";

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IEyeManager _eye = default!;

    private readonly Dictionary<EntityUid, RsiDirection> _directions = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DirectionalLimbLayerComponent, OrganGotInsertedEvent>(OnOrganChanged);
        SubscribeLocalEvent<DirectionalLimbLayerComponent, OrganGotRemovedEvent>(OnOrganChanged);
        SubscribeLocalEvent<VisualBodyComponent, ComponentShutdown>(OnBodyShutdown);

        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<DirectionalLimbLayerComponent, OrganComponent>();
        while (query.MoveNext(out _, out _, out var organ))
        {
            if (organ.Body is not { } body ||
                !TryComp(body, out SpriteComponent? sprite) ||
                !TryComp(body, out TransformComponent? transform) ||
                transform.MapID == MapId.Nullspace)
                continue;

            var angle = (_transform.GetWorldRotation(transform) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();
            if (TryGetRenderDirection((body, sprite), angle, out var direction))
                Update((body, sprite), direction);
        }
    }

    public void Update(Entity<SpriteComponent?> body, Direction direction)
    {
        Update(body, direction switch
        {
            Direction.East => RsiDirection.East,
            Direction.West => RsiDirection.West,
            Direction.North => RsiDirection.North,
            _ => RsiDirection.South,
        });
    }

    private void Update(Entity<SpriteComponent?> body, RsiDirection direction)
    {
        var directionChanged = !_directions.TryGetValue(body.Owner, out var oldDirection) || oldDirection != direction;
        _directions[body.Owner] = direction;
        if (directionChanged)
        {
            _sprite.RemoveLayer(body, LegOverlay, false);
            _sprite.RemoveLayer(body, FootOverlay, false);
        }

        var rightInFront = direction == RsiDirection.East;
        if (!rightInFront && direction != RsiDirection.West)
            return;

        SyncOverlay(body,
            rightInFront ? HumanoidVisualLayers.RLeg : HumanoidVisualLayers.LLeg,
            HumanoidVisualLayers.RLeg,
            HumanoidVisualLayers.LLeg,
            LegOverlay);
        SyncOverlay(body,
            rightInFront ? HumanoidVisualLayers.RFoot : HumanoidVisualLayers.LFoot,
            HumanoidVisualLayers.RFoot,
            HumanoidVisualLayers.LFoot,
            FootOverlay);
    }

    private void OnOrganChanged(Entity<DirectionalLimbLayerComponent> ent, ref OrganGotInsertedEvent args)
    {
        _directions.Remove(args.Target);
    }

    private void OnOrganChanged(Entity<DirectionalLimbLayerComponent> ent, ref OrganGotRemovedEvent args)
    {
        _directions.Remove(args.Target);
        _sprite.RemoveLayer(args.Target, LegOverlay, false);
        _sprite.RemoveLayer(args.Target, FootOverlay, false);
    }

    private void OnBodyShutdown(Entity<VisualBodyComponent> ent, ref ComponentShutdown args)
    {
        _directions.Remove(ent.Owner);
    }

    private bool TryGetRenderDirection(Entity<SpriteComponent?> body, Angle angle, out RsiDirection direction)
    {
        if (TryGetRenderDirection(body, HumanoidVisualLayers.RLeg, angle, out direction) ||
            TryGetRenderDirection(body, HumanoidVisualLayers.LLeg, angle, out direction))
            return true;

        direction = default;
        return false;
    }

    private bool TryGetRenderDirection(
        Entity<SpriteComponent?> body,
        HumanoidVisualLayers key,
        Angle angle,
        out RsiDirection direction)
    {
        if (_sprite.TryGetLayer(body, key, out var layer, false) && layer.ActualState is { } state)
        {
            direction = SpriteComponent.Layer.GetDirection(state.RsiDirections, angle).OffsetRsiDir(layer.DirOffset);
            return true;
        }

        direction = default;
        return false;
    }

    private void SyncOverlay(
        Entity<SpriteComponent?> body,
        HumanoidVisualLayers sourceKey,
        HumanoidVisualLayers rightKey,
        HumanoidVisualLayers leftKey,
        string overlayKey)
    {
        if (!_sprite.TryGetLayer(body, sourceKey, out var source, false) || source.Blank || !source.Visible)
        {
            _sprite.RemoveLayer(body, overlayKey, false);
            return;
        }

        if (_sprite.TryGetLayer(body, overlayKey, out var overlay, false) && LayersMatch(source, overlay))
            return;

        _sprite.RemoveLayer(body, overlayKey, false);
        if (!_sprite.LayerMapTryGet(body, rightKey, out var rightIndex, false) ||
            !_sprite.LayerMapTryGet(body, leftKey, out var leftIndex, false))
            return;

        var index = _sprite.AddLayer(body, new SpriteComponent.Layer(source, body.Comp!), Math.Max(rightIndex, leftIndex) + 1);
        _sprite.LayerMapSet(body, overlayKey, index);
    }

    private static bool LayersMatch(SpriteComponent.Layer source, SpriteComponent.Layer overlay)
    {
        return source.State == overlay.State &&
               source.ActualRsi == overlay.ActualRsi &&
               source.Texture == overlay.Texture &&
               source.Visible == overlay.Visible &&
               source.Color == overlay.Color &&
               source.Scale == overlay.Scale &&
               source.Rotation == overlay.Rotation &&
               source.Offset == overlay.Offset &&
               source.RenderingStrategy == overlay.RenderingStrategy;
    }
}

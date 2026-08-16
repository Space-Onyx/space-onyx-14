using Content.Shared._Onyx.Swimming.Components;
using Content.Shared._Onyx.Swimming.Systems;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Swimming;

public sealed partial class OceanSwimmingVisualSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> OceanSubmersionShader = "OnyxOceanSubmersion";
    private const string OceanSubmersionPostShaderId = "ocean-submersion";
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedOceanSwimmingSystem _sharedOceanSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private ShaderPrototype _shaderPrototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shaderPrototype = _prototypes.Index(OceanSubmersionShader);
        SubscribeLocalEvent<SpriteComponent, MapInitEvent>(OnSpriteInit);
        SubscribeLocalEvent<SpriteComponent, ComponentShutdown>(OnSpriteShutdown);
        SubscribeLocalEvent<SpriteComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<OceanMapComponent, ComponentInit>(OnOceanMapChanged);
        SubscribeLocalEvent<OceanMapComponent, AfterAutoHandleStateEvent>(OnOceanMapChanged);
        SubscribeLocalEvent<OceanMapComponent, ComponentShutdown>(OnOceanMapShutdown);
        SubscribeLocalEvent<CanMoveInAirComponent, ComponentInit>(OnSwimmingExclusionChanged);
        SubscribeLocalEvent<CanMoveInAirComponent, ComponentRemove>(OnSwimmingExclusionChanged);
    }

    public override void Shutdown()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        base.Shutdown();
    }

    private void OnSpriteShutdown(Entity<SpriteComponent> ent, ref ComponentShutdown args)
    {
        if (_shaders.Remove(ent.Owner, out var shader))
            shader.Dispose();
    }

    private void OnSpriteInit(Entity<SpriteComponent> ent, ref MapInitEvent args)
    {
        UpdateVisual(ent);
    }

    private void OnParentChanged(Entity<SpriteComponent> ent, ref EntParentChangedMessage args)
    {
        UpdateVisual(ent);
    }

    private void OnOceanMapChanged(Entity<OceanMapComponent> ent, ref ComponentInit args)
    {
        UpdateMapVisuals(ent);
    }

    private void OnOceanMapChanged(Entity<OceanMapComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateMapVisuals(ent);
    }

    private void OnOceanMapShutdown(Entity<OceanMapComponent> ent, ref ComponentShutdown args)
    {
        foreach (var uid in new List<EntityUid>(_shaders.Keys))
        {
            if (TryComp(uid, out TransformComponent? xform) && xform.MapUid == ent.Owner &&
                TryComp<SpriteComponent>(uid, out var sprite))
                RemoveShader((uid, sprite));
        }
    }

    private void OnSwimmingExclusionChanged<T>(Entity<T> ent, ref ComponentInit args) where T : Component
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            UpdateVisual((ent.Owner, sprite));
    }

    private void OnSwimmingExclusionChanged<T>(Entity<T> ent, ref ComponentRemove args) where T : Component
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            UpdateVisual((ent.Owner, sprite));
    }

    private void UpdateMapVisuals(Entity<OceanMapComponent> map)
    {
        var query = EntityQueryEnumerator<SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var xform))
        {
            if (xform.MapUid == map.Owner)
                UpdateVisual((uid, sprite), xform, map.Comp);
        }
    }

    private void UpdateVisual(Entity<SpriteComponent> ent)
    {
        var xform = Transform(ent);

        OceanMapComponent? ocean = null;
        if (xform.MapUid is { } mapUid)
            TryComp(mapUid, out ocean);
        UpdateVisual(ent, xform, ocean);
    }

    private void UpdateVisual(Entity<SpriteComponent> ent, TransformComponent xform, OceanMapComponent? ocean)
    {
        var isSwimming = ocean != null &&
                         xform.GridUid == null &&
                         !_sharedOceanSystem.ShouldIgnoreOceanSwimming(ent);

        UpdateVisual(ent, ocean, isSwimming);
    }

    private void UpdateVisual(Entity<SpriteComponent> ent, OceanMapComponent? ocean, bool isSwimming)
    {
        if (isSwimming)
        {
            if (TryApplyShader(ent, ent.Comp, out var shader))
            {
                shader.SetParameter("submersionDepth", Math.Clamp(ocean!.SubmersionDepth, 0f, 1f));
                shader.SetParameter("submergedAlpha", Math.Clamp(ocean.SubmergedAlpha, 0f, 1f));
            }
        }
        else
            RemoveShader(ent);
    }

    private bool TryApplyShader(EntityUid uid, SpriteComponent sprite, out ShaderInstance shader)
    {
        if (!_shaders.TryGetValue(uid, out shader!))
        {
            shader = _shaderPrototype.InstanceUnique();
            _shaders.Add(uid, shader);
        }

        _sprite.SetPostShader(new Entity<SpriteComponent?>(uid, sprite),
            new SpriteComponent.PostShaderArgs(OceanSubmersionPostShaderId, shader));

        return true;
    }

    private void RemoveShader(Entity<SpriteComponent> ent)
    {
        if (!_shaders.Remove(ent.Owner, out var shader))
            return;

        _sprite.RemovePostShader(ent.AsNullable(), OceanSubmersionPostShaderId);
        shader.Dispose();
    }
}

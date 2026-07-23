using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.Overlays;
using Content.Shared.Body;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Overlays;

public sealed partial class ThermalVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;

    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly ContainerSystem _container;
    private readonly SharedPointLightSystem _light;
    private readonly List<ThermalVisionRenderEntry> _entries = [];

    private EntityUid? _lightEntity;

    public ThermalVisionComponent? Component;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ThermalVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _container = _entityManager.System<ContainerSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _light = _entityManager.System<SharedPointLightSystem>();
        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eyeManager.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Component is not { Enabled: true } comp ||
            args.Viewport.Eye is not { } eye ||
            _playerManager.LocalEntity is not { } player ||
            !_entityManager.TryGetComponent(player, out TransformComponent? playerTransform))
        {
            ResetLight();
            return;
        }

        EnsureLight(player, playerTransform, comp);
        var alpha = comp.PulseTime <= 0f ? 1f : Math.Clamp(comp.PulseRemaining / comp.PulseTime, 0f, 1f);

        var mapId = eye.Position.MapId;
        var eyeRotation = eye.Rotation;
        _entries.Clear();

        var query = _entityManager.EntityQueryEnumerator<BodyComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var body, out var sprite, out var transform))
        {
            if (!body.ThermalVisibility || !sprite.Visible ||
                _entityManager.TryGetComponent(uid, out StealthComponent? stealth) && stealth.ThermalsImmune)
                continue;

            var rendered = uid;
            if (_container.TryGetOuterContainer(uid, transform, out var container) &&
                _entityManager.TryGetComponent(container.Owner, out SpriteComponent? outerSprite) &&
                _entityManager.TryGetComponent(container.Owner, out TransformComponent? outerTransform))
            {
                rendered = container.Owner;
                sprite = outerSprite;
                transform = outerTransform;
            }

            if (_entries.Any(entry => entry.Entity.Owner == rendered))
                continue;

            _entries.Add(new ThermalVisionRenderEntry((rendered, sprite, transform), mapId, eyeRotation));
        }

        foreach (var entry in _entries)
            Render(entry, args.WorldHandle, comp, alpha);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void EnsureLight(EntityUid player, TransformComponent playerTransform, ThermalVisionComponent comp)
    {
        _lightEntity ??= _entityManager.SpawnAttachedTo(null, playerTransform.Coordinates);
        _transform.SetParent(_lightEntity.Value, player);
        var light = _entityManager.EnsureComponent<PointLightComponent>(_lightEntity.Value);
        _light.SetRadius(_lightEntity.Value, comp.LightRadius, light);
        var alpha = comp.PulseTime <= 0f ? 1f : Math.Clamp(comp.PulseRemaining / comp.PulseTime, 0f, 1f);
        _light.SetEnergy(_lightEntity.Value, alpha, light);
        _light.SetColor(_lightEntity.Value, comp.Color, light);
    }

    private void Render(ThermalVisionRenderEntry entry,
        DrawingHandleWorld handle,
        ThermalVisionComponent comp,
        float alpha)
    {
        var (uid, sprite, transform) = entry.Entity;
        if (transform.MapID != entry.Map || !sprite.Visible)
            return;

        var originalColor = sprite.Color;
        var layers = new Dictionary<int, (ShaderInstance? Shader, Color Color)>();
        var allLayers = sprite.AllLayers.ToList();
        for (var i = 0; i < allLayers.Count; i++)
        {
            if (allLayers[i] is not SpriteComponent.Layer { Visible: true } layer)
                continue;

            layers[i] = (layer.Shader, layer.Color);
            layer.Shader = null;
            _sprite.LayerSetColor(layer, Color.White.WithAlpha(layer.Color.A));
        }

        _sprite.SetColor((uid, sprite), Color.White.WithAlpha(alpha));
        handle.UseShader(_prototypeManager.Index<ShaderPrototype>(comp.ThermalShader).Instance());
        _sprite.RenderSprite((uid, sprite), handle, entry.EyeRotation,
            _transform.GetWorldRotation(transform), _transform.GetWorldPosition(transform));
        handle.UseShader(null);
        _sprite.SetColor((uid, sprite), originalColor);

        foreach (var (index, data) in layers)
        {
            ((SpriteComponent.Layer) sprite[index]).Shader = data.Shader;
            _sprite.LayerSetColor((uid, sprite), index, data.Color);
        }
    }

    public void ResetLight()
    {
        if (_lightEntity == null)
            return;

        _entityManager.DeleteEntity(_lightEntity);
        _lightEntity = null;
    }
}

public readonly record struct ThermalVisionRenderEntry(
    Entity<SpriteComponent, TransformComponent> Entity,
    MapId? Map,
    Angle EyeRotation);

using Content.Shared._Onyx.Phasing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx;

public sealed partial class PhasingSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> PhasingShaderId = "Phasing";
    private const string PhasingPostShaderId = "phasing";

    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private ShaderPrototype _shaderPrototype = default!;
    private readonly Dictionary<EntityUid, ShaderInstance> _activeShaders = new();

    public override void Initialize()
    {
        base.Initialize();
        _shaderPrototype = _prototype.Index(PhasingShaderId);

        SubscribeLocalEvent<PhasingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PhasingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PhasingComponent, AfterAutoHandleStateEvent>(OnState);
    }

    public override void Shutdown()
    {
        foreach (var uid in new List<EntityUid>(_activeShaders.Keys))
            RemoveShader(uid);
        base.Shutdown();
    }

    private void OnStartup(Entity<PhasingComponent> ent, ref ComponentStartup args)
    {
        ApplyState(ent);
    }

    private void OnShutdown(Entity<PhasingComponent> ent, ref ComponentShutdown args)
    {
        RemoveShader(ent.Owner);
    }

    private void OnState(Entity<PhasingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyState(ent);
    }

    private void ApplyState(Entity<PhasingComponent> ent, SpriteComponent? sprite = null)
    {
        if (!ent.Comp.Enabled)
        {
            RemoveShader(ent.Owner, sprite);
            return;
        }

        if (!Resolve(ent.Owner, ref sprite, false))
            return;

        if (!_activeShaders.TryGetValue(ent.Owner, out var instance))
        {
            instance = _shaderPrototype.InstanceUnique();
            _activeShaders.Add(ent.Owner, instance);
        }

        ApplyShaderParams(ent.Comp, instance);
        _sprite.SetPostShader((ent.Owner, sprite), new SpriteComponent.PostShaderArgs(PhasingPostShaderId, instance));
    }

    private void RemoveShader(EntityUid uid, SpriteComponent? sprite = null)
    {
        if (!_activeShaders.Remove(uid, out var instance))
            return;

        if (Resolve(uid, ref sprite, false))
            _sprite.RemovePostShader((uid, sprite), PhasingPostShaderId);

        instance.Dispose();
    }

    private static void ApplyShaderParams(PhasingComponent component, ShaderInstance shader)
    {
        var bandMin = MathF.Max(1f, component.BandMin);
        var bandMax = MathF.Max(bandMin, component.BandMax);

        shader.SetParameter("bandMin", bandMin);
        shader.SetParameter("bandMax", bandMax);
        shader.SetParameter("animationSpeed", MathF.Max(0f, component.AnimationSpeed));
        shader.SetParameter("distortionStrength", MathF.Max(0f, component.DistortionStrength));
        shader.SetParameter("glitchFrequency", Math.Clamp(component.GlitchFrequency, 0f, 1f));
        shader.SetParameter("bandSplitStrength", MathF.Max(0f, component.BandSplitStrength));
        shader.SetParameter("bandSplitFrequency", Math.Clamp(component.BandSplitFrequency, 0f, 1f));
    }
}

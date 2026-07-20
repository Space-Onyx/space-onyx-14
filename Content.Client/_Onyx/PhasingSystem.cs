using Content.Shared._Onyx.Phasing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx;

public sealed partial class PhasingSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> PhasingShaderId = "Phasing";

    [Dependency] private IPrototypeManager _prototype = default!;

    private ShaderPrototype _shaderPrototype = default!;
    private readonly Dictionary<EntityUid, ActiveShader> _activeShaders = new();

    public override void Initialize()
    {
        base.Initialize();
        _shaderPrototype = _prototype.Index(PhasingShaderId);

        SubscribeLocalEvent<PhasingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PhasingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PhasingComponent, AfterAutoHandleStateEvent>(OnState);
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

        if (_activeShaders.TryGetValue(ent.Owner, out var active))
        {
            ApplyShaderParams(ent.Comp, active.Instance);
            return;
        }

        var instance = _shaderPrototype.InstanceUnique();
        ApplyShaderParams(ent.Comp, instance);
        _activeShaders.Add(ent.Owner, new ActiveShader(instance, sprite.PostShader));
        sprite.PostShader = instance;
    }

    private void RemoveShader(EntityUid uid, SpriteComponent? sprite = null)
    {
        if (!_activeShaders.Remove(uid, out var active))
            return;

        if (Resolve(uid, ref sprite, false) && sprite.PostShader == active.Instance)
            sprite.PostShader = active.Previous;

        active.Instance.Dispose();
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

    private sealed record ActiveShader(ShaderInstance Instance, ShaderInstance? Previous);
}

using Content.Shared._Onyx.Holograms;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Holograms;

public sealed partial class HologramVisualizerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly ProtoId<ShaderPrototype> _shaderId = "Holographic";
    private ShaderPrototype? _shader;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HologramVisualsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HologramVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        base.Shutdown();
    }

    private void OnInit(Entity<HologramVisualsComponent> ent, ref ComponentInit args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
        {
            var shader = (_shader ??= _prototypes.Index(_shaderId)).InstanceUnique();
            _shaders[ent.Owner] = shader;
            sprite.PostShader = shader;
        }
    }

    private void OnShutdown(Entity<HologramVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent.Owner, out var shader))
            return;

        if (TryComp(ent, out SpriteComponent? sprite) && sprite.PostShader == shader)
            sprite.PostShader = null;
        shader.Dispose();
    }
}

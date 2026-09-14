using Content.Shared._Onyx.Holograms;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Holograms;

public sealed partial class HologramVisualizerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly ProtoId<ShaderPrototype> _shaderId = "Holographic";
    private const string HologramPostShaderId = "hologram";
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
        foreach (var uid in new List<EntityUid>(_shaders.Keys))
            RemoveShader(uid);
        base.Shutdown();
    }

    private void OnInit(Entity<HologramVisualsComponent> ent, ref ComponentInit args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
        {
            var shader = (_shader ??= _prototypes.Index(_shaderId)).InstanceUnique();
            _shaders[ent.Owner] = shader;
            _sprite.SetPostShader((ent.Owner, sprite), new SpriteComponent.PostShaderArgs(HologramPostShaderId, shader));
        }
    }

    private void OnShutdown(Entity<HologramVisualsComponent> ent, ref ComponentShutdown args)
    {
        RemoveShader(ent);
    }

    private void RemoveShader(EntityUid uid, SpriteComponent? sprite = null)
    {
        if (!_shaders.Remove(uid, out var shader))
            return;

        if (Resolve(uid, ref sprite, false))
            _sprite.RemovePostShader((uid, sprite), HologramPostShaderId);
        shader.Dispose();
    }
}

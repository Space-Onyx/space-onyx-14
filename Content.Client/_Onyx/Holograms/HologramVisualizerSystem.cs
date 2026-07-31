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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HologramVisualsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HologramVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<HologramVisualsComponent> ent, ref ComponentInit args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            sprite.PostShader = (_shader ??= _prototypes.Index(_shaderId)).InstanceUnique();
    }

    private void OnShutdown(Entity<HologramVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            sprite.PostShader = null;
    }
}

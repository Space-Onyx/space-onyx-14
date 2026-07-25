using Content.Shared.EntityEffects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.EntityEffects.Effects.EntitySpawning;

public sealed partial class SpawnRandomQuantityEntityEffectSystem
    : EntityEffectSystem<TransformComponent, SpawnRandomQuantity>
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnRandomQuantity> args)
    {
        if (_net.IsClient || args.Effect.MaxEntities <= 0)
            return;

        var quantity = _random.Next(args.Effect.MaxEntities) + 1;
        for (var i = 0; i < quantity; i++)
            SpawnNextToOrDrop(args.Effect.Entity, entity.Owner, entity.Comp);
    }
}

public sealed partial class SpawnRandomQuantity : EntityEffectBase<SpawnRandomQuantity>
{
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField]
    public int MaxEntities = 1;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-spawn-random-quantity",
            ("chance", Probability),
            ("entname", prototype.Index<EntityPrototype>(Entity).Name),
            ("amount", MaxEntities));
}

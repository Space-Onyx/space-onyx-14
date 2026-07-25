using Content.Shared.EntityEffects;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.EntityEffects.Effects.Area;

public sealed partial class ApplyEffectsNearbyEntityEffectSystem
    : EntityEffectSystem<TransformComponent, ApplyEffectsNearby>
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<ApplyEffectsNearby> args)
    {
        if (_net.IsClient || args.Effect.Range < 0f)
            return;

        foreach (var target in _lookup.GetEntitiesInRange(entity.Owner, args.Effect.Range))
            _effects.ApplyEffects(target, args.Effect.Effects, args.Scale, args.User);
    }
}

public sealed partial class ApplyEffectsNearby : EntityEffectBase<ApplyEffectsNearby>
{
    [DataField]
    public float Range = 5f;

    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}

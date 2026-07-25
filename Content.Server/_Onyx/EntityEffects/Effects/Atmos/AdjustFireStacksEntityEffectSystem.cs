using Content.Server.Atmos.EntitySystems;
using Content.Shared._Onyx.EntityEffects.Effects.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.EntityEffects;

namespace Content.Server._Onyx.EntityEffects.Effects.Atmos;

public sealed partial class AdjustFireStacksEntityEffectSystem
    : EntityEffectSystem<FlammableComponent, AdjustFireStacks>
{
    [Dependency] private FlammableSystem _flammable = default!;

    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<AdjustFireStacks> args)
    {
        _flammable.AdjustFireStacks(entity, args.Effect.Amount * args.Scale, entity.Comp, args.Effect.Ignite);
    }
}

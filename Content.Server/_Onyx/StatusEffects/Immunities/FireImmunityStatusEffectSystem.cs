using Content.Server.Atmos.EntitySystems;
using Content.Shared._Onyx.StatusEffects.Immunities;
using Content.Shared.Atmos.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Server._Onyx.StatusEffects.Immunities;

public sealed partial class FireImmunityStatusEffectSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireImmunityStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
    }

    private void OnApplied(Entity<FireImmunityStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (TryComp<FlammableComponent>(args.Target, out var flammable))
            _flammable.Extinguish(args.Target, flammable);
    }
}

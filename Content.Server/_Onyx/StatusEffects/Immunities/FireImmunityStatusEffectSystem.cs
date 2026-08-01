using Content.Server.Atmos.EntitySystems;
using Content.Shared._Onyx.StatusEffects.Immunities;
using Content.Shared.Atmos.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Temperature;

namespace Content.Server._Onyx.StatusEffects.Immunities;

public sealed partial class FireImmunityStatusEffectSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireImmunityStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<StatusEffectContainerComponent, BeforeHeatExchangeEvent>(OnBeforeHeatExchange);
    }

    private void OnApplied(Entity<FireImmunityStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (TryComp<FlammableComponent>(args.Target, out var flammable))
            _flammable.Extinguish(args.Target, flammable);
    }

    private void OnBeforeHeatExchange(Entity<StatusEffectContainerComponent> entity, ref BeforeHeatExchangeEvent args)
    {
        if (EntityManager.System<StatusEffectsSystem>().HasEffectComp<HighTemperatureImmunityStatusEffectComponent>(entity))
            args.HeatTransferModifier = 0f;
    }
}

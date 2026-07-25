using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.Chemistry;

public sealed partial class TakeStaminaDamageEntityEffectSystem : EntityEffectSystem<StaminaComponent, TakeStaminaDamage>
{
    [Dependency] private SharedStaminaSystem _stamina = default!;

    protected override void Effect(Entity<StaminaComponent> entity, ref EntityEffectEvent<TakeStaminaDamage> args)
    {
        if (args.Scale != 1f)
            return;

        _stamina.TakeStaminaDamage(entity, args.Effect.Amount, entity.Comp, visual: false);
    }
}

public sealed partial class TakeStaminaDamage : EntityEffectBase<TakeStaminaDamage>
{
    [DataField]
    public float Amount = 10f;

    // ponytail: Vanilla has no overtime stamina mode; port stunmeta before using this value.
    [DataField]
    public bool Immediate;
}

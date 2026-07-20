using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.EntityEffects.EffectConditions;

public sealed partial class TotalDamage : EntityConditionBase<TotalDamage>
{
    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) =>
        Loc.GetString("reagent-effect-condition-guidebook-total-damage",
            ("max", Max == FixedPoint2.MaxValue ? (float) int.MaxValue : Max.Float()),
            ("min", Min.Float()));
}

public sealed partial class TotalDamageEntityConditionSystem : EntityConditionSystem<DamageableComponent, TotalDamage>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<TotalDamage> args)
    {
        var total = _damageable.GetTotalDamage(entity.AsNullable());
        args.Result = total >= args.Condition.Min && total <= args.Condition.Max;
    }
}

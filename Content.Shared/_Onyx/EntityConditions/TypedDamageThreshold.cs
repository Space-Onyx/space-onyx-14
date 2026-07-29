using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions;

/// <summary>
/// Checks damage only in the configured damage types or complete damage groups.
/// </summary>
public sealed partial class TypedDamageThresholdSystem : EntityConditionSystem<DamageableComponent, TypedDamageThreshold>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<TypedDamageThreshold> args)
    {
        // ponytail: Typed reagent thresholds require the legacy numeric damage view until damage-model conditions replace it.
#pragma warning disable CS0618
        var currentDamage = _damageable.GetAllDamage(entity.AsNullable());
#pragma warning restore CS0618
        var comparison = new DamageSpecifier(args.Condition.Damage);
        foreach (var group in ProtoMan.EnumeratePrototypes<DamageGroupPrototype>())
        {
            var lowest = FixedPoint2.MaxValue;
            foreach (var type in group.DamageTypes)
            {
                if (!comparison.DamageDict.TryGetValue(type, out var value))
                {
                    lowest = FixedPoint2.Zero;
                    break;
                }

                lowest = FixedPoint2.Min(lowest, value);
            }

            if (lowest == FixedPoint2.Zero || lowest == FixedPoint2.MaxValue)
                continue;

            if (currentDamage.TryGetDamageInGroup(group, out var total) && total > lowest * group.DamageTypes.Count)
            {
                args.Result = !args.Condition.Inverse;
                return;
            }

            foreach (var type in group.DamageTypes)
                comparison.DamageDict[type] -= lowest;
        }

        comparison.ExclusiveAdd(-currentDamage);
        comparison = -comparison;
        args.Result = comparison.AnyPositive() ^ args.Condition.Inverse;
    }
}

public sealed partial class TypedDamageThreshold : EntityConditionBase<TypedDamageThreshold>
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public bool Inverse;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

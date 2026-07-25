using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chemistry;

public sealed partial class StaminaDamageConditionSystem : EntityConditionSystem<StaminaComponent, StaminaDamageCondition>
{
    [Dependency] private SharedStaminaSystem _stamina = default!;

    protected override void Condition(Entity<StaminaComponent> entity, ref EntityConditionEvent<StaminaDamageCondition> args)
    {
        var damage = _stamina.GetStaminaDamage(entity, entity.Comp);
        args.Result = damage > args.Condition.Min && damage < args.Condition.Max;
    }
}

public sealed partial class StaminaDamageCondition : EntityConditionBase<StaminaDamageCondition>
{
    [DataField]
    public float Min = -1f;

    [DataField]
    public float Max = float.PositiveInfinity;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

using Content.Shared._Onyx.Salvage.Bosses.Megafauna.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Bosses.Megafauna.Conditions;

public sealed partial class ActionAvailableCondition : MegafaunaCondition
{
    [DataField(required: true)]
    public EntProtoId ActionId;

    public override bool EvaluateImplementation(MegafaunaCalculationBaseArgs args)
    {
        var entMan = args.EntityManager;
        var actionSys = entMan.System<SharedActionsSystem>();
        Entity<ActionComponent>? action = null;
        foreach (var candidate in actionSys.GetActions(args.Entity))
        {
            if (entMan.GetComponent<MetaDataComponent>(candidate).EntityPrototype?.ID != ActionId.Id)
                continue;
            action = candidate;
            break;
        }
        if (action == null)
            return false;
        return action.Value.Comp.Enabled && !actionSys.IsCooldownActive(action.Value.Comp);
    }
}

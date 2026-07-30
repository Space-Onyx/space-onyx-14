using Content.Shared._Onyx.Salvage.Bosses.Megafauna.Components;
using Content.Shared._Onyx.Salvage.Bosses.Megafauna.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Salvage.Bosses.Megafauna.Selectors;

/// <summary>
/// Performs an action and if required, tries to get target positions
/// from <see cref="MegafaunaAiTargetingComponent"/>.
/// </summary>
public sealed partial class PerformActionSelector : MegafaunaSelector
{
    [DataField]
    public EntProtoId ActionId;

    protected override float InvokeImplementation(MegafaunaCalculationBaseArgs args)
    {
        var entMan = args.EntityManager;
        var actionSys = entMan.System<SharedActionsSystem>();
        var megafaunaSys = entMan.System<MegafaunaSystem>();

        Entity<ActionComponent>? action = null;
        foreach (var candidate in actionSys.GetActions(args.Entity))
        {
            if (entMan.GetComponent<MetaDataComponent>(candidate).EntityPrototype?.ID != ActionId.Id)
                continue;
            action = candidate;
            break;
        }
        if (action == null)
        {
            DebugTools.Assert($"{entMan.ToPrettyString(args.Entity)}'s AI failed to get an action with ID {ActionId}!");
            return FailDelay;
        }

        var ev = megafaunaSys.GetPerformEvent(args.Entity, action.Value.Owner, actionSys);

        if (!action.Value.Comp.Enabled || actionSys.IsCooldownActive(action.Value.Comp))
        {
            DebugTools.Assert($"{entMan.ToPrettyString(args.Entity)}'s AI failed to perform action {entMan.ToPrettyString(action.Value.Owner)} with ID {ActionId}!");
            return FailDelay;
        }

        actionSys.PerformAction(args.Entity, action.Value, ev);

        return DelaySelector.Get(args);
    }
}

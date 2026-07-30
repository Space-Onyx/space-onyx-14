using System.Numerics;
using Content.Shared._Onyx.Salvage.Bosses.Megafauna.Components;
using Content.Shared._Onyx.Salvage.Bosses.Megafauna.Events;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Salvage.Bosses.Megafauna.Systems;

public sealed partial class MegafaunaSystem
{
    public void StartupMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaStartupEvent());
        ent.Comp.Active = true;
    }

    public void ShutdownMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaShutdownEvent());
        ent.Comp.Active = false;
    }

    public void KillMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaKilledEvent());
        ent.Comp.Active = false;
    }

    /// <summary>
    /// Helper method that fills an action event for megafauna AI.
    /// </summary>
    public BaseActionEvent? GetPerformEvent(EntityUid boss, EntityUid action, SharedActionsSystem actions)
    {
        var targetingComp = CompOrNull<MegafaunaAiTargetingComponent>(boss);
        var ev = actions.GetEvent(action);
        if (ev is WorldTargetActionEvent world && targetingComp?.TargetCoords is {} coords)
        {
            world.Target = coords;
            world.Entity = targetingComp.TargetEnt;
        }
        else if (ev is EntityTargetActionEvent entity && targetingComp?.TargetEnt is {} target)
            entity.Target = target;
        return ev;
    }

    public void PickRandomPosition(MegafaunaCalculationBaseArgs args, float radius)
    {
        // TODO add an option to not pick any obstructed coordinates

        var uid = args.Entity;
        var mapId = Transform(uid).MapID;

        var diameter = radius * 2f;
        var randomVector = new Vector2(
            (float) args.Random.NextDouble() * diameter - radius,
            (float) args.Random.NextDouble() * diameter - radius);
        var position = _xform.GetWorldPosition(uid) + randomVector;
        var newMapCoords = new MapCoordinates(position, mapId);
        var coords = _xform.ToCoordinates(newMapCoords);

        var comp = EnsureComp<MegafaunaAiTargetingComponent>(args.Entity);
        comp.TargetEnt = null;
        comp.TargetCoords = coords;
    }
}

using Content.Server.Shuttles.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Shuttles.Events;
using Content.Shared.Power;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    private void OnSetInertiaDampening(Entity<ShuttleConsoleComponent> ent, ref SetInertiaDampeningRequest args)
    {
        var targetConsole = GetDroneConsole(ent.Owner) ?? ent.Owner;
        var grid = Transform(targetConsole).GridUid;
        if (grid == null || !TrySetInertiaDampening(grid.Value, args.Mode))
        {
            RefreshShuttleConsoles(grid ?? ent.Owner);
            return;
        }

        RefreshShuttleConsoles(grid.Value);
    }

    public bool TrySetInertiaDampening(EntityUid grid, InertiaDampeningMode mode)
    {
        if (!TryComp<ShuttleComponent>(grid, out var shuttle) ||
            mode is InertiaDampeningMode.None || !Enum.IsDefined(mode) ||
            mode == InertiaDampeningMode.Anchor && HasComp<FTLComponent>(grid))
            return false;

        shuttle.DampeningMode = mode;
        SetDampening(shuttle, mode);
        return true;
    }

    private void UpdateDampeningPower(EntityUid console)
    {
        var grid = Transform(console).GridUid;
        if (grid == null || !TryComp<ShuttleComponent>(grid, out var shuttle))
            return;

        var anyPowered = false;
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid && this.IsPowered(uid, EntityManager))
            {
                anyPowered = true;
                break;
            }
        }

        SetDampening(shuttle, anyPowered ? shuttle.DampeningMode : InertiaDampeningMode.Anchor);
        RefreshShuttleConsoles(grid.Value);
    }

    private static void SetDampening(ShuttleComponent shuttle, InertiaDampeningMode mode)
    {
        shuttle.BodyModifier = mode switch
        {
            InertiaDampeningMode.Cruise => 0.0075f,
            InertiaDampeningMode.Anchor => 2f,
            _ => 0.25f,
        };

        if (shuttle.DampingModifier != 0f)
            shuttle.DampingModifier = shuttle.BodyModifier;
    }

    private void UpdateDampeningState(NavInterfaceState state, EntityUid? grid)
    {
        state.DampeningMode = TryComp<ShuttleComponent>(grid, out var shuttle)
            ? shuttle.DampeningMode
            : InertiaDampeningMode.Dampen;
        state.InFtl = HasComp<FTLComponent>(grid);
    }
}

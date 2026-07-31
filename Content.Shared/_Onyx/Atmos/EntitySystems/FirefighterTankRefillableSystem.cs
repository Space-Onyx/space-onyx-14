using Content.Shared._Onyx.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.Atmos.EntitySystems;

public sealed partial class FirefighterTankRefillableSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FirefighterTankRefillableComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FirefighterTankRefillableComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { Valid: true } target || !args.CanReach)
            return;

        if (TryComp(target, out ReagentTankComponent? tank) && tank.TankType == ReagentTankType.Fuel)
            return;

        if (!_solutions.TryGetDrainableSolution(target, out var sourceEnt, out var source) ||
            !_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var destinationEnt, out var destination))
            return;

        var quantity = FixedPoint2.Min(destination.AvailableVolume, source.Volume);
        if (quantity > 0)
        {
            var drained = _solutions.Drain(target, sourceEnt.Value, quantity);
            _solutions.TryAddSolution(destinationEnt.Value, drained);
            _audio.PlayPredicted(ent.Comp.RefillSound, ent, args.User);
            _popup.PopupClient(Loc.GetString("firefighter-nozzle-component-after-interact-refilled-message"), ent, args.User);
        }
        else if (destination.AvailableVolume <= 0)
        {
            _popup.PopupClient(Loc.GetString("firefighter-nozzle-component-already-full"), ent, args.User);
        }
        else
        {
            _popup.PopupClient(Loc.GetString("firefighter-nozzle-component-no-water-in-tank", ("owner", target)), ent, args.User);
        }

        args.Handled = true;
    }
}

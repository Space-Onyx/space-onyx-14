using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class CartridgeAutoinjectorSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CartridgeAutoinjectorComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<CartridgeAutoinjectorComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CartridgeAutoinjectorComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CartridgeAutoinjectorComponent, InjectorInjectionCompletedEvent>(OnInjectionCompleted);
        SubscribeLocalEvent<TargetBeforeInjectEvent>(OnBeforeInject);
    }

    private void OnAfterInteract(Entity<CartridgeAutoinjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is { } target && !HasComp<BloodstreamComponent>(target))
            args.Handled = true;
    }

    private void OnInserted(Entity<CartridgeAutoinjectorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != "item" ||
            !TryComp<SolutionCartridgeComponent>(args.Entity, out var cartridge) ||
            !TryComp<InjectorComponent>(ent, out var injector) ||
            !_solution.ResolveSolution(ent.Owner, cartridge.TargetSolution, ref injector.Solution, out _))
            return;

        _solution.TryAddSolution(injector.Solution.Value, cartridge.Solution);
    }

    private void OnRemoved(Entity<CartridgeAutoinjectorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != "item" ||
            !TryComp<SolutionCartridgeComponent>(args.Entity, out var cartridge) ||
            !TryComp<InjectorComponent>(ent, out var injector) ||
            !_solution.ResolveSolution(ent.Owner, cartridge.TargetSolution, ref injector.Solution, out _))
            return;

        _solution.RemoveAllSolution(injector.Solution.Value);
    }

    private void OnBeforeInject(ref TargetBeforeInjectEvent args)
    {
        if (!HasComp<CartridgeAutoinjectorComponent>(args.UsedInjector) ||
            !_solution.TryGetInjectableSolution(args.TargetGettingInjected, out _, out var targetSolution) ||
            targetSolution.AvailableVolume >= 10)
            return;

        args.Cancel();
    }

    private void OnInjectionCompleted(Entity<CartridgeAutoinjectorComponent> ent, ref InjectorInjectionCompletedEvent args)
    {
        if (_net.IsClient || args.Amount != 10 || !_container.TryGetContainer(ent, "item", out var container))
            return;

        if (TryComp<InjectorComponent>(ent, out var injector) &&
            _solution.ResolveSolution(ent.Owner, injector.SolutionName, ref injector.Solution, out _))
            _solution.RemoveAllSolution(injector.Solution.Value);

        _container.CleanContainer(container);
    }
}

using Content.Shared.Buckle.Components;
using Content.Shared.Buckle;
using Content.Shared.Hands;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;

namespace Content.Shared.Vehicle;

public sealed partial class VehicleSystem
{
    // <Onyx-VehicleHands-edited>
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    [SubscribeLocalEvent]
    private void OnVehicleStrapAttempt(Entity<StrapVehicleComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled || !_vehicleQuery.TryComp(ent, out var vehicle))
            return;

        for (var i = 0; i < vehicle.RequiredHands; i++)
        {
            if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Buckle.Owner, false))
                continue;

            args.Cancelled = true;
            _virtualItem.DeleteInHandsMatching(args.Buckle.Owner, ent.Owner);
            return;
        }
    }
    // </Onyx-VehicleHands-edited>

    [SubscribeLocalEvent]
    private void OnVehicleStrapped(Entity<StrapVehicleComponent> ent, ref StrappedEvent args)
    {
        if (!_vehicleQuery.TryComp(ent, out var vehicle))
            return;
        TrySetOperator((ent, vehicle), args.Buckle);
    }

    [SubscribeLocalEvent]
    private void OnVehicleUnstrapped(Entity<StrapVehicleComponent> ent, ref UnstrappedEvent args)
    {
        if (!_vehicleQuery.TryComp(ent, out var vehicle))
            return;

        if (vehicle.Operator != args.Buckle)
            return;

        TryRemoveOperator((ent, vehicle));
        _virtualItem.DeleteInHandsMatching(args.Buckle.Owner, ent.Owner); // <Onyx-VehicleHands-edited>
    }

    // <Onyx-VehicleHands-edited>
    [SubscribeLocalEvent]
    private void OnVehicleVirtualItemDeleted(Entity<VehicleComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (ent.Comp.Operator != args.User)
            return;

        _buckle.TryUnbuckle(args.User, args.User, popup: false);
    }
    // </Onyx-VehicleHands-edited>

    [SubscribeLocalEvent]
    private void OnContainerEntInserted(Entity<ContainerVehicleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != ent.Comp.ContainerId)
            return;

        if (!_vehicleQuery.TryComp(ent, out var vehicle))
            return;

        TrySetOperator((ent, vehicle), args.Entity);
    }

    [SubscribeLocalEvent]
    private void OnContainerEntRemoved(Entity<ContainerVehicleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != ent.Comp.ContainerId)
            return;

        if (!_vehicleQuery.TryComp(ent, out var vehicle))
            return;

        if (vehicle.Operator != args.Entity)
            return;

        TryRemoveOperator((ent, vehicle));
    }
}

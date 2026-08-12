using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.VendingMachines.Components;
using Content.Shared.Wires;

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem
{
    public bool TryAccessMachine(EntityUid uid, VendingMachineRestockComponent restock, VendingMachineComponent machineComponent, EntityUid user, EntityUid target)
    {
        if (TryComp<WiresPanelComponent>(target, out var panel) && panel.Open) return true;
        Popup.PopupCursor(Loc.GetString("vending-machine-restock-needs-panel-open", ("this", uid), ("user", user), ("target", target)), user);
        return false;
    }

    public bool TryMatchPackageToMachine(EntityUid uid, VendingMachineRestockComponent component, VendingMachineComponent machineComponent, EntityUid user, EntityUid target)
    {
        if (component.CanRestock.Contains(machineComponent.PackPrototypeId)) return true;
        Popup.PopupCursor(Loc.GetString("vending-machine-restock-invalid-inventory", ("this", uid), ("user", user), ("target", target)), user);
        return false;
    }

    public void TryRestockInventory(EntityUid uid, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent)) return;
        RestockInventoryFromPrototype(uid, vendComponent);
        Dirty(uid, vendComponent);
    }

    [SubscribeLocalEvent]
    private void OnAfterInteract(EntityUid uid, VendingMachineRestockComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || args.Handled || !TryComp<VendingMachineComponent>(target, out var machineComponent) || !TryMatchPackageToMachine(uid, component, machineComponent, args.User, target) || !TryAccessMachine(uid, component, machineComponent, args.User, target)) return;
        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, component.RestockDelay, new RestockDoAfterEvent(), target, target: target, used: uid) { BreakOnMove = true, BreakOnDamage = true, NeedHand = true };
        if (!_doAfter.TryStartDoAfter(doAfter)) return;
        Popup.PopupEntity(Loc.GetString("vending-machine-restock-start-self", ("target", target)), Loc.GetString("vending-machine-restock-start-others", ("user", Identity.Entity(args.User, EntityManager)), ("target", target)), target, args.User, PopupType.Medium);
        if (!Timing.IsFirstTimePredicted) return;
        Audio.Stop(machineComponent.RestockStream);
        machineComponent.RestockStream = Audio.PlayPredicted(component.SoundRestockStart, target, args.User)?.Entity;
    }

    [SubscribeLocalEvent]
    private void OnRestockDoAfter(Entity<VendingMachineComponent> ent, ref RestockDoAfterEvent args)
    {
        if (args.Cancelled) { if (Timing.IsFirstTimePredicted) ent.Comp.RestockStream = Audio.Stop(ent.Comp.RestockStream); return; }
        if (args.Handled || args.Used == null || !TryComp<VendingMachineRestockComponent>(args.Used, out var restock)) return;
        TryRestockInventory(ent, ent.Comp);
        Popup.PopupEntity(Loc.GetString("vending-machine-restock-done-self", ("target", ent)), Loc.GetString("vending-machine-restock-done-others", ("user", Identity.Entity(args.User, EntityManager)), ("target", ent)), ent, args.User, PopupType.Medium);
        Audio.PlayPredicted(restock.SoundRestockDone, ent, args.User);
        PredictedQueueDel(args.Used.Value);
    }
}

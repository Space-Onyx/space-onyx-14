using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared._Onyx.Bitrunning.Systems;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;

namespace Content.Server.VendingMachines;

public sealed partial class VendingMachineSystem
{
    [Dependency] private BitrunningPointsSystem _bitrunningPoints = default!;

    private bool IsBitrunningPointsVendor(EntityUid uid) => HasComp<BitrunningPointsVendorComponent>(uid);

    private bool TryAuthorizedBitrunningPointsVend(EntityUid uid, EntityUid sender,
        VendingMachineComponent component, VendingMachineInventoryEntry entry)
    {
        if (!IsBitrunningPointsVendor(uid))
            return false;

        return TryAuthorizedPointVend(uid, sender, component, entry,
            price => _bitrunningPoints.TryRemovePoints(sender, price),
            "vending-machine-component-no-bitrunning-points");
    }

    private bool TryAuthorizedPointVend(EntityUid uid, EntityUid sender, VendingMachineComponent component,
        VendingMachineInventoryEntry entry, Func<int, bool> trySpend, string insufficientPointsLocId)
    {
        if (!TryComp<VendingMachineEjectComponent>(uid, out var eject) ||
            !IsAuthorized(uid, sender, component) || eject.Ejecting || component.Broken || !_receiver.IsPowered(uid))
            return true;

        if ((!component.InfiniteStock && entry.Amount == 0) || string.IsNullOrEmpty(entry.ID))
        {
            Deny((uid, component), sender, eject);
            return true;
        }

        var price = entry.Price;
        if (price < 0 || !component.AllForFree && price > 0 && !trySpend(price))
        {
            UpdateVendingMachineInterfaceState(uid, component);
            Popup.PopupEntity(Loc.GetString(insufficientPointsLocId), uid, sender);
            Deny((uid, component), sender, eject);
            return true;
        }

        TryEjectVendorItem(uid, entry.Type, entry.ID, ShouldThrowVendItem((uid, eject)), sender, component, eject);
        UpdateVendingMachineInterfaceState(uid, component);
        return true;
    }
}

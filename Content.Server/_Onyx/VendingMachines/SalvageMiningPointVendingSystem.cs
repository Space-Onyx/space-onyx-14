using Content.Shared._Onyx.Materials;
using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;

namespace Content.Server.VendingMachines;

public sealed partial class VendingMachineSystem
{
    [Dependency] private MiningPointsSystem _miningPoints = default!;

    private bool IsSalvageMiningPointVendor(EntityUid uid)
    {
        return HasComp<SalvageMiningPointVendorComponent>(uid);
    }

    private bool TryAuthorizedSalvageMiningPointVend(EntityUid uid, EntityUid sender,
        VendingMachineComponent component, VendingMachineInventoryEntry entry)
    {
        if (!IsSalvageMiningPointVendor(uid))
            return false;

        if (!TryComp<VendingMachineEjectComponent>(uid, out var eject) ||
            !IsAuthorized(uid, sender, component) || eject.Ejecting || component.Broken || !_receiver.IsPowered(uid))
            return true;

        if ((!component.InfiniteStock && entry.Amount == 0) || string.IsNullOrEmpty(entry.ID))
        {
            Deny((uid, component), sender, eject);
            return true;
        }

        var price = entry.Price;
        if (price < 0 ||
            !component.AllForFree &&
            (!_miningPoints.TryFindIdCard(sender, out var card) || price > 0 && !_miningPoints.TrySpend(card, price)))
        {
            UpdateVendingMachineInterfaceState(uid, component);
            Popup.PopupEntity(Loc.GetString("vending-machine-component-no-mining-points"), uid, sender);
            Deny((uid, component), sender, eject);
            return true;
        }

        TryEjectVendorItem(uid, entry.Type, entry.ID, ShouldThrowVendItem((uid, eject)), sender, component, eject);
        UpdateVendingMachineInterfaceState(uid, component);
        return true;
    }
}

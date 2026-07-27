using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared._Onyx.Materials;
using Content.Shared.Advertise.Components;
using Content.Shared.VendingMachines;

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

        if (!IsAuthorized(uid, sender, component) || component.Ejecting || component.Broken || !_receiver.IsPowered(uid))
            return true;

        if ((!component.InfiniteStock && entry.Amount == 0) || string.IsNullOrEmpty(entry.ID))
        {
            Deny((uid, component), sender);
            return true;
        }

        var price = entry.Price;
        if (price < 0 ||
            !component.AllForFree &&
            (!_miningPoints.TryFindIdCard(sender, out var card) || price > 0 && !_miningPoints.TrySpend(card, price)))
        {
            UpdateVendingMachineInterfaceState(uid, component);
            Popup.PopupEntity(Loc.GetString("vending-machine-component-no-mining-points"), uid, sender);
            Deny((uid, component), sender);
            return true;
        }

        component.NextItemCount = 1;
        component.EjectEnd = Timing.CurTime + component.EjectDelay;
        component.NextItemToEject = entry.ID;
        component.ThrowNextItem = component.CanShoot;

        if (TryComp(uid, out SpeakOnUIClosedComponent? speakComponent))
            _speakOn.TrySetFlag((uid, speakComponent));

        Dirty(uid, component);
        UpdateUI((uid, component));
        TryUpdateVisualState((uid, component));
        UpdateVendingMachineInterfaceState(uid, component);
        return true;
    }
}

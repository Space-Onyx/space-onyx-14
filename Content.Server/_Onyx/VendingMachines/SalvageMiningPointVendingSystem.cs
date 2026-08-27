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

        return TryAuthorizedPointVend(uid, sender, component, entry,
            price => _miningPoints.TryFindIdCard(sender, out var card) && _miningPoints.TrySpend(card, price),
            "vending-machine-component-no-mining-points");
    }
}

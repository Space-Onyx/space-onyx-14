using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Onyx.Clothing.Modsuits.Systems;

public sealed partial class RaveOverlayInventoryRelaySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<RaveOverlayComponent>>(OnRefresh);
    }

    private void OnRefresh(Entity<InventoryComponent> inventory, ref RefreshEquipmentHudEvent<RaveOverlayComponent> args) =>
        _inventory.RelayEvent(inventory, ref args);
}

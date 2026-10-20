// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Clothing;
using Content.Shared._Onyx.Disease;
using Content.Shared._Onyx.Disease.Components;
using Content.Shared._Onyx.Flashbang;
using Content.Shared._Onyx.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Onyx.Inventory;

public sealed partial class EquipmentInventoryRelaySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, DiseaseIncomingSpreadAttemptEvent>(OnIncomingSpread);
        SubscribeLocalEvent<InventoryComponent, DiseaseOutgoingSpreadAttemptEvent>(OnOutgoingSpread);
        SubscribeLocalEvent<InventoryComponent, GetStandingUpTimeMultiplierEvent>(OnStandingUpTime);
        SubscribeLocalEvent<InventoryComponent, GetFlashbangedEvent>(OnFlashbanged);
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<ThermalVisionComponent>>(OnThermalVision);
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<ShowDiseaseIconsComponent>>(OnDiseaseIcons);
    }

    private void OnIncomingSpread(Entity<InventoryComponent> ent, ref DiseaseIncomingSpreadAttemptEvent args)
    {
        _inventory.RelayEvent(ent, args);
    }

    private void OnOutgoingSpread(Entity<InventoryComponent> ent, ref DiseaseOutgoingSpreadAttemptEvent args)
    {
        _inventory.RelayEvent(ent, args);
    }

    private void OnStandingUpTime(Entity<InventoryComponent> ent, ref GetStandingUpTimeMultiplierEvent args)
    {
        _inventory.RelayEvent(ent, args);
    }

    private void OnFlashbanged(Entity<InventoryComponent> ent, ref GetFlashbangedEvent args)
    {
        _inventory.RelayEvent(ent, args);
    }

    private void OnThermalVision(Entity<InventoryComponent> ent, ref RefreshEquipmentHudEvent<ThermalVisionComponent> args)
    {
        _inventory.RelayEvent(ent, ref args);
    }

    private void OnDiseaseIcons(Entity<InventoryComponent> ent, ref RefreshEquipmentHudEvent<ShowDiseaseIconsComponent> args)
    {
        _inventory.RelayEvent(ent, ref args);
    }
}

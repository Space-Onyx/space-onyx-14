// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Items.Systems;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Onyx.Clothing;

public sealed partial class ClothingAccessoryHolderSystem : EntitySystem
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
    }

    private void OnContainerChanged(Entity<ClothingAccessoryHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Slots.ContainsKey(args.Container.ID))
            _item.VisualsChanged(ent.Owner);
    }

    private void OnContainerChanged(Entity<ClothingAccessoryHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.Slots.ContainsKey(args.Container.ID))
            _item.VisualsChanged(ent.Owner);
    }

    private void OnGetEquipmentVisuals(Entity<ClothingAccessoryHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        foreach (var (id, definition) in ent.Comp.Slots)
        {
            if (definition.EquippedState is not { } state ||
                _itemSlots.GetItemOrNull(ent.Owner, id) is not { } accessory ||
                !TryComp(accessory, out SpriteComponent? sprite) ||
                sprite.BaseRSI is not { } rsi)
            {
                continue;
            }

            args.Layers.Add(($"{id}-{accessory}", new PrototypeLayerData
            {
                RsiPath = rsi.Path.ToString(),
                State = state,
            }));
        }
    }
}

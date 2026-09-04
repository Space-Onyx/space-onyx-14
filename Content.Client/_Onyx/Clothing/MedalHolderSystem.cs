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

public sealed partial class MedalHolderSystem : EntitySystem
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedalHolderComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<MedalHolderComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<MedalHolderComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
    }

    private void OnContainerChanged(Entity<MedalHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MedalHolderComponent.SlotId)
            _item.VisualsChanged(ent.Owner);
    }

    private void OnContainerChanged(Entity<MedalHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == MedalHolderComponent.SlotId)
            _item.VisualsChanged(ent.Owner);
    }

    private void OnGetEquipmentVisuals(Entity<MedalHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, MedalHolderComponent.SlotId) is not { } medal ||
            !TryComp(medal, out SpriteComponent? sprite) ||
            sprite.BaseRSI is not { } rsi)
        {
            return;
        }

        args.Layers.Add(($"{MedalHolderComponent.SlotId}-{medal}", new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = "equipped-NECK",
        }));
    }
}

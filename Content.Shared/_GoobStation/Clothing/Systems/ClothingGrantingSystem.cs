// SPDX-FileCopyrightText: 2024 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 VMSolidus <evilexecutive@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._GoobStation.Clothing.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._GoobStation.Clothing.Systems;

public sealed partial class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingGrantComponentComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(Entity<ClothingGrantComponentComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(ent, out var clothing) || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        foreach (var (name, data) in ent.Comp.Components)
        {
            var newComp = (Component) _componentFactory.GetComponent(name);
            if (HasComp(args.EquipTarget, newComp.GetType()))
                continue;

            newComp.Owner = args.EquipTarget;
            var serialized = (object) newComp;
            _serializationManager.CopyTo(data.Component, ref serialized);
            AddComp(args.EquipTarget, (Component) serialized!);
            ent.Comp.Active[name] = true;
        }
    }

    private void OnUnequip(Entity<ClothingGrantComponentComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var (name, _) in ent.Comp.Components)
        {
            if (!ent.Comp.Active.GetValueOrDefault(name))
                continue;

            var component = (Component) _componentFactory.GetComponent(name);
            RemComp(args.EquipTarget, component.GetType());
            ent.Comp.Active[name] = false;
        }
    }
}

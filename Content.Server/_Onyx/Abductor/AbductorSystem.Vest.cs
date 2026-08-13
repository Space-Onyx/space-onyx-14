// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Abductor;
using Content.Shared._Onyx.ItemSwitch;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth.Components;

namespace Content.Server._Onyx.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    public void InitializeVest()
    {
        SubscribeLocalEvent<AbductorVestComponent, AfterInteractEvent>(OnVestInteract);
        SubscribeLocalEvent<AbductorVestComponent, ItemSwitchedEvent>(OnItemSwitch);
        SubscribeLocalEvent<AbductorVestComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<AbductorVestComponent, GotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<AbductorVestComponent> ent, ref GotEquippedEvent args)
    {
        if (!HasComp<StealthComponent>(args.EquipTarget) && ent.Comp.CurrentState != AbductorArmorMode.Combat)
        {
            AddComp<StealthComponent>(args.EquipTarget);
            AddComp<StealthOnMoveComponent>(args.EquipTarget);
        }
    }

    private void OnUnequipped(Entity<AbductorVestComponent> ent, ref GotUnequippedEvent args)
    {
        if (HasComp<StealthComponent>(args.EquipTarget))
        {
            RemComp<StealthComponent>(args.EquipTarget);
            RemComp<StealthOnMoveComponent>(args.EquipTarget);
        }
    }

    private void OnItemSwitch(EntityUid uid, AbductorVestComponent component, ref ItemSwitchedEvent args)
    {

        if (Enum.TryParse<AbductorArmorMode>(args.State, ignoreCase: true, out var State))
            component.CurrentState = State;

        var user = Transform(uid).ParentUid;

        if (State == AbductorArmorMode.Combat)
        {
            if (TryComp<ClothingComponent>(uid, out var clothingComponent))
                _clothing.SetEquippedPrefix(uid, "combat", clothingComponent);

            if (HasComp<MobStateComponent>(user) && HasComp<StealthComponent>(user))
            {
                RemComp<StealthComponent>(user);
                RemComp<StealthOnMoveComponent>(user);
            }
        }
        else
        {
            if (TryComp<ClothingComponent>(uid, out var clothingComponent))
                _clothing.SetEquippedPrefix(uid, null, clothingComponent);

            if (HasComp<MobStateComponent>(user) && !HasComp<StealthComponent>(user))
            {
                AddComp<StealthComponent>(user);
                AddComp<StealthOnMoveComponent>(user);
            }
        }
    }

    private void OnVestInteract(Entity<AbductorVestComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach) return;
        if (!args.Target.HasValue) return;

        if (TryComp<AbductorConsoleComponent>(args.Target, out var console))
        {
            console.Armor = GetNetEntity(ent);
            _popup.PopupEntity(Loc.GetString("abductors-ui-vest-linked"), args.User);
            return;
        }
    }
}

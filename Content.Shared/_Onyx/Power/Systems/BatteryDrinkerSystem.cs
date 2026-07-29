// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Whitelist;
using Content.Shared._Onyx.Power.Components;
using Content.Shared._Onyx.Silicons.Charge;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Power.Systems;

public sealed partial class BatteryDrinkerSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BatteryDrinkerSourceComponent, GetVerbsEvent<AlternativeVerb>>(AddDrinkVerb);
        SubscribeLocalEvent<PowerCellSlotComponent, GetVerbsEvent<AlternativeVerb>>(AddDrinkVerb);
        SubscribeLocalEvent<BatteryDrinkerComponent, BatteryDrinkerDoAfterEvent>(OnDoAfter);
    }

    private void AddDrinkVerb<TComp>(Entity<TComp> ent, ref GetVerbsEvent<AlternativeVerb> args)
        where TComp : Component
    {
        if (!args.CanAccess || !args.CanInteract ||
            !TryComp(args.User, out BatteryDrinkerComponent? drinker) ||
            _whitelist.IsWhitelistPass(drinker.Blacklist, ent) ||
            !SearchForBattery(args.User, out _) ||
            !SearchForSource(ent, out var source))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => DrinkBattery(source.Value, user, drinker),
            Text = Loc.GetString("battery-drinker-verb-drink"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/smite.svg.192dpi.png")),
            Priority = -5,
        });
    }

    private void DrinkBattery(EntityUid source, EntityUid user, BatteryDrinkerComponent drinker)
    {
        if (!TryComp(source, out BatteryDrinkerSourceComponent? sourceComp))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            drinker.DrinkSpeed * sourceComp.DrinkSpeedMulti,
            new BatteryDrinkerDoAfterEvent(),
            user,
            source)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 1.35f,
            RequireCanInteract = true,
            CancelDuplicate = false,
        });
    }

    private void OnDoAfter(Entity<BatteryDrinkerComponent> ent, ref BatteryDrinkerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } source || !_net.IsServer ||
            !TryComp(source, out BatteryComponent? sourceBattery) ||
            !TryComp(source, out BatteryDrinkerSourceComponent? sourceComp) ||
            !SearchForBattery(ent, out var drinkerUid) ||
            !TryComp(drinkerUid, out BatteryComponent? drinkerBattery))
            return;

        args.Handled = true;
        var amount = MathF.Min(ent.Comp.DrinkMultiplier * 1000f, _battery.GetCharge((source, sourceBattery)));
        amount = MathF.Min(amount, drinkerBattery.MaxCharge - _battery.GetCharge((drinkerUid.Value, drinkerBattery)));
        if (sourceComp.MaxAmount > 0)
            amount = MathF.Min(amount, sourceComp.MaxAmount.Value);

        if (amount <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("battery-drinker-empty", ("target", source)), ent, ent);
            return;
        }

        _battery.UseCharge((source, sourceBattery), amount);
        _battery.ChangeCharge((drinkerUid.Value, drinkerBattery), amount);
        _popup.PopupEntity(Loc.GetString("ipc-recharge-tip"), ent, ent, PopupType.SmallCaution);
        if (sourceComp.DrinkSound != null)
            _audio.PlayPvs(sourceComp.DrinkSound, source);
        Spawn("EffectSparks", Transform(source).Coordinates);
    }

    private bool SearchForSource(EntityUid ent, [NotNullWhen(true)] out EntityUid? source)
    {
        if (TryGetCellSlot(ent, out var slot) && slot.HasItem && HasComp<BatteryDrinkerSourceComponent>(slot.Item))
        {
            source = slot.Item;
            return true;
        }

        if (HasComp<BatteryDrinkerSourceComponent>(ent) && HasComp<BatteryComponent>(ent))
        {
            source = ent;
            return true;
        }

        source = null;
        return false;
    }

    private bool SearchForBattery(EntityUid ent, [NotNullWhen(true)] out EntityUid? battery)
    {
        if (TryGetCellSlot(ent, out var slot))
        {
            battery = slot.Item;
            return slot.HasItem && HasComp<BatteryComponent>(slot.Item);
        }

        battery = ent;
        return HasComp<BatteryComponent>(ent);
    }

    private bool TryGetCellSlot(EntityUid ent, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        if (!HasComp<ItemSlotsComponent>(ent))
            return false;

        if (TryComp(ent, out PowerCellSlotComponent? powerCellSlot) &&
            _itemSlots.TryGetSlot(ent, powerCellSlot.CellSlotId, out slot))
            return true;

        return HasComp<MagazineAmmoProviderComponent>(ent) &&
            _itemSlots.TryGetSlot(ent, "gun_magazine", out slot);
    }
}

[Serializable, NetSerializable]
public sealed partial class BatteryDrinkerDoAfterEvent : SimpleDoAfterEvent;

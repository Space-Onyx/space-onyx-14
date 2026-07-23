// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Construction;

public sealed partial class UpgradeKitSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedWiresSystem _wires = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UpgradeKitComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<UpgradeKitComponent, UpgradeKitDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<UpgradeKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not {} target)
            return;

        args.Handled = true;
        var user = args.User;
        if (!CanUpgrade(ent, target, user))
            return;

        if (!_wires.IsPanelOpen(target))
        {
            _popup.PopupClient(Loc.GetString("construction-step-condition-wire-panel-open"), target, user);
            return;
        }

        ent.Comp.SoundStream = _audio.PlayPredicted(ent.Comp.UpgradeSound, ent, user)?.Entity;
        Dirty(ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            ent.Comp.Delay,
            new UpgradeKitDoAfterEvent(),
            ent,
            target,
            ent));
    }

    private void OnDoAfter(Entity<UpgradeKitComponent> ent, ref UpgradeKitDoAfterEvent args)
    {
        ent.Comp.SoundStream = _audio.Stop(ent.Comp.SoundStream);
        if (args.Cancelled || args.Handled || args.Args.Target is not {} target)
            return;

        args.Handled = true;
        if (!CanUpgrade(ent, target, args.Args.User))
            return;

        EntityManager.AddComponents(target, ent.Comp.Components);
        if (_net.IsServer)
            QueueDel(ent);
    }

    private bool CanUpgrade(Entity<UpgradeKitComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_whitelist.IsWhitelistFail(ent.Comp.Whitelist, target) &&
            _whitelist.IsWhitelistFail(ent.Comp.Blacklist, target))
            return true;

        _popup.PopupClient(Loc.GetString("upgrade-kit-invalid-target"), target, user);
        return false;
    }
}

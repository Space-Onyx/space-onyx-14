// SPDX-FileCopyrightText: 2025 Baptr0b0t <152836416+Baptr0b0t@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ted Lukin <66275205+pheenty@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.RelayedDeathrattle;
using Content.Server.Popups;
using Content.Shared._Onyx.CrewMonitoring;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.CrewMonitoring;

public sealed partial class CrewMonitorScanningSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedSubdermalImplantSystem _implantSystem = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const string CommandTrackerImplant = "CommandTrackingImplant";
    private const string CommandTrackerImplantName = "command tracking implant";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitorScanningComponent, AfterInteractEvent>(OnScanAttempt);
        SubscribeLocalEvent<CrewMonitorScanningComponent, CrewMonitorScanningDoAfterEvent>(OnScanComplete);
    }

    private void OnScanAttempt(EntityUid uid, CrewMonitorScanningComponent comp, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<HumanoidProfileComponent>(args.Target))
            return;

        var userName = Identity.Entity(args.User, EntityManager);
        _popup.PopupEntity(Loc.GetString("injector-component-injecting-user"), args.Target.Value, args.User);
        if (args.User != args.Target.Value)
        {
            _popup.PopupEntity(
                Loc.GetString("implanter-component-implanting-target", ("user", userName)),
                args.User,
                args.Target.Value,
                PopupType.LargeCaution);
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            comp.DoAfterTime,
            new CrewMonitorScanningDoAfterEvent(),
            uid,
            args.Target,
            uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnScanComplete(EntityUid uid, CrewMonitorScanningComponent comp, CrewMonitorScanningDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        var target = args.Target.Value;
        var name = Identity.Name(target, EntityManager, args.User);

        if (comp.ScannedEntities.Contains(target))
        {
            var msg = Loc.GetString(
                "implanter-component-implant-already",
                ("implant", CommandTrackerImplantName),
                ("target", name));
            _popup.PopupEntity(msg, target, args.User);
            return;
        }

        if (_whitelist.IsWhitelistFail(comp.Whitelist, target))
        {
            var msg = Loc.GetString(
                "implanter-component-implant-failed",
                ("implant", CommandTrackerImplantName),
                ("target", name));
            _popup.PopupEntity(msg, target, args.User);
            return;
        }

        comp.ScannedEntities.Add(target);
        _implantSystem.AddImplant(target, CommandTrackerImplant);

        if (comp.ApplyDeathrattle)
            EnsureComp<RelayedDeathrattleComponent>(target).Target = uid;

        args.Handled = true;
    }
}

// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 SX_7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Construction.Completions;
using Content.Server.Inventory;
using Content.Server.Popups;
using Content.Shared._Onyx.VentCrawling;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.VentCrawling;

public sealed partial class VentCrawlerTubeSystem : EntitySystem
{
    [Dependency] private SharedVentCrawableSystem _ventCrawableSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private ServerInventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VentCrawlerTubeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VentCrawlerTubeComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<VentCrawlerTubeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VentCrawlerTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<VentCrawlerTubeComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<VentCrawlerTubeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VentCrawlerTubeComponent, ConstructionBeforeDeleteEvent>(OnDeconstruct);
        SubscribeLocalEvent<VentCrawlerEntryComponent, GetVerbsEvent<AlternativeVerb>>(AddClimbVerb);
        SubscribeLocalEvent<VentCrawlerComponent, EnterVentDoAfterEvent>(OnEnterDoAfter);
    }

    private void AddClimbVerb(EntityUid uid, VentCrawlerEntryComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<VentCrawlerComponent>(args.User, out var crawler)
            || HasComp<BeingVentCrawlerComponent>(args.User)
            || !Transform(uid).Anchored
            || !TryComp<VentCrawlerTubeComponent>(uid, out var tube)
            || !tube.Connected
            || TryComp<WeldableComponent>(uid, out var weldable) && weldable.IsWelded)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryEnter(uid, args.User, crawler),
            Text = Loc.GetString("ventcrawling-enter-pipe-network"),
        });
    }

    private void OnEnterDoAfter(EntityUid uid, VentCrawlerComponent component, EnterVentDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        if (!component.AllowInventory && IsHoldingItems(args.Args.Used.Value))
            return;

        if (!TryInsert(args.Args.Target.Value, args.Args.Used.Value))
            _popup.PopupEntity(Loc.GetString("ventcrawling-enter-failed"), args.Args.Used.Value);
        args.Handled = true;
    }

    private void TryEnter(EntityUid uid, EntityUid user, VentCrawlerComponent crawler)
    {
        if (TryComp<WeldableComponent>(uid, out var weldable) && weldable.IsWelded)
        {
            _popup.PopupEntity(Loc.GetString("entity-storage-component-welded-shut-message"), user);
            return;
        }

        if (!crawler.AllowInventory && IsHoldingItems(user))
            return;

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, user, crawler.EnterDelay, new EnterVentDoAfterEvent(), user, uid, user)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
        });
    }

    private void OnComponentInit(EntityUid uid, VentCrawlerTubeComponent tube, ComponentInit args)
        => tube.Contents = _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);

    private void OnComponentRemove(EntityUid uid, VentCrawlerTubeComponent tube, ComponentRemove args) => DisconnectTube((uid, tube));
    private void OnShutdown(EntityUid uid, VentCrawlerTubeComponent tube, ComponentShutdown args) => DisconnectTube((uid, tube));
    private void OnDeconstruct(EntityUid uid, VentCrawlerTubeComponent tube, ConstructionBeforeDeleteEvent args) => DisconnectTube((uid, tube));
    private void OnBreak(EntityUid uid, VentCrawlerTubeComponent tube, BreakageEventArgs args) => DisconnectTube((uid, tube));
    private void OnMapInit(EntityUid uid, VentCrawlerTubeComponent tube, MapInitEvent args) => UpdateAnchored((uid, tube), Transform(uid).Anchored);
    private void OnAnchorChange(EntityUid uid, VentCrawlerTubeComponent tube, ref AnchorStateChangedEvent args) => UpdateAnchored((uid, tube), args.Anchored);

    private void UpdateAnchored(Entity<VentCrawlerTubeComponent> tube, bool anchored)
    {
        if (anchored)
            tube.Comp.Connected = true;
        else
            DisconnectTube(tube);
    }

    private void DisconnectTube(Entity<VentCrawlerTubeComponent> tube)
    {
        if (!tube.Comp.Connected)
            return;

        tube.Comp.Connected = false;
        var query = EntityQueryEnumerator<VentCrawlerHolderComponent>();
        while (query.MoveNext(out var entity, out var holder))
        {
            if (holder.CurrentTube != tube.Owner && holder.NextTube != tube.Owner)
                continue;

            var ev = new VentCrawlingExitEvent();
            RaiseLocalEvent(entity, ref ev);
        }
    }

    private bool TryInsert(EntityUid uid, EntityUid entity, VentCrawlerEntryComponent? entry = null)
    {
        if (!Resolve(uid, ref entry)
            || !TryComp<VentCrawlerComponent>(entity, out var crawler)
            || crawler.InTube
            || HasComp<BeingVentCrawlerComponent>(entity)
            || !Transform(uid).Anchored
            || !TryComp<VentCrawlerTubeComponent>(uid, out var tube)
            || !tube.Connected
            || TryComp<WeldableComponent>(uid, out var weldable) && weldable.IsWelded
            || !crawler.AllowInventory && IsHoldingItems(entity))
            return false;

        var holder = Spawn(entry.HolderPrototypeId, Transform(uid).Coordinates);
        var holderComponent = Comp<VentCrawlerHolderComponent>(holder);
        if (!_ventCrawableSystem.TryInsert(holder, entity, holderComponent))
        {
            QueueDel(holder);
            return false;
        }

        holderComponent.FirstEntry = true;
        _mover.ResetCamera(entity);
        _mover.SetRelay(entity, holder);
        if (TryComp<InputMoverComponent>(entity, out var input))
            _ventCrawableSystem.SetMovementInput(holderComponent, input.HeldMoveButtons);
        crawler.InTube = true;
        Dirty(entity, crawler);
        _eye.RefreshVisibilityMask(entity);
        return _ventCrawableSystem.EnterTube(holder, uid, holderComponent);
    }

    private bool IsHoldingItems(EntityUid uid)
    {
        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out _) || _inventory.TryGetSlotEntity(uid, "back", out _))
        {
            _popup.PopupEntity(Loc.GetString("ventcrawling-block-enter-reson-equiptment"), uid);
            return true;
        }

        if (_hands.EnumerateHeld(uid).Any())
        {
            _popup.PopupEntity(Loc.GetString("ventcrawling-block-enter-reson-hand"), uid);
            return true;
        }

        return false;
    }
}

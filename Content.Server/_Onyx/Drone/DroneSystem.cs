using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Shared._Onyx.Drone;
using Content.Shared.Alert;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Roles;
using Content.Shared.SubFloor;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Shared.Eye;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Drone;

public sealed partial class DroneSystem : SharedDroneSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DroneComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DroneComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<DroneComponent, UserOpenActivatableUIAttemptEvent>(OnActivateUiAttempt);
        SubscribeLocalEvent<DroneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DroneComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DroneComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<DroneComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<DroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        SubscribeLocalEvent<DroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<DroneComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<DroneComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<DroneComponent, StartingGearEquippedEvent>(OnStartingGearEquipped);
        SubscribeLocalEvent<DroneComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<DroneComponent>();
        while (query.MoveNext(out var uid, out var drone))
        {
            if (_timing.CurTime < drone.NextBatteryUpdate)
                continue;

            drone.NextBatteryUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            UpdateBatteryAlert((uid, drone));
            if (!_powerCell.HasDrawCharge(uid))
                _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }

    private void OnMapInit(Entity<DroneComponent> ent, ref MapInitEvent args)
    {
        UpdateBatteryAlert(ent);
        if (!TryComp<MindContainerComponent>(ent, out var mind) || !mind.HasMind)
            _powerCell.SetDrawEnabled(ent.Owner, false);
    }

    private void OnStartingGearEquipped(Entity<DroneComponent> ent, ref StartingGearEquippedEvent args)
        => RefreshTrayScannerVisibility(ent);

    private void OnPlayerAttached(Entity<DroneComponent> ent, ref PlayerAttachedEvent args)
        => RefreshTrayScannerVisibility(ent);

    private void RefreshTrayScannerVisibility(Entity<DroneComponent> ent)
    {
        var count = 0;
        foreach (var item in _inventory.GetHandOrInventoryEntities(ent.Owner, SlotFlags.POCKET))
        {
            if (HasComp<TrayScannerComponent>(item))
                count++;
        }

        if (count == 0)
            RemComp<TrayScannerUserComponent>(ent);
        else
            EnsureComp<TrayScannerUserComponent>(ent).Count = count;

        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnUseAttempt(Entity<DroneComponent> ent, ref UseAttemptEvent args)
    {
        if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, args.Used))
        {
            args.Cancel();
            ShowTimedPopup(ent, "drone-cant-use", PopupType.SmallCaution);
            return;
        }

        if (!_whitelist.IsWhitelistPass(ent.Comp.Whitelist, args.Used) && NonDronesInRange(ent))
            ShowProximityPopup(ent, "drone-too-close");
    }

    private void OnActivateUiAttempt(Entity<DroneComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, args.Target))
            args.Cancel();
    }

    private void OnExamined(Entity<DroneComponent> ent, ref ExaminedEvent args)
    {
        var active = TryComp<MindContainerComponent>(ent, out var mind) && mind.HasMind;
        args.PushMarkup(Loc.GetString(active ? "drone-active" : "drone-dormant"));
    }

    private void OnMobStateChanged(Entity<DroneComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _gibbing.Gib(ent);
    }

    private void OnPowerCellChanged(Entity<DroneComponent> ent, ref PowerCellChangedEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        UpdateBatteryAlert(ent);
        if (!_powerCell.HasDrawCharge(ent.Owner))
            _mobState.ChangeMobState(ent, MobState.Dead);
    }

    private void OnPowerCellSlotEmpty(Entity<DroneComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (!TerminatingOrDeleted(ent))
            _mobState.ChangeMobState(ent, MobState.Dead);
    }

    private void OnMindAdded(Entity<DroneComponent> ent, ref MindAddedMessage args)
    {
        SetStatus(ent, DroneStatus.On);
        _popup.PopupEntity(Loc.GetString("drone-activated"), ent, PopupType.Large);
        _powerCell.SetDrawEnabled(ent.Owner, true);
    }

    private void OnMindRemoved(Entity<DroneComponent> ent, ref MindRemovedMessage args)
    {
        SetStatus(ent, DroneStatus.Off);
        EnsureComp<GhostTakeoverAvailableComponent>(ent);
        _powerCell.SetDrawEnabled(ent.Owner, false);
    }

    private void OnEmoteAttempt(Entity<DroneComponent> ent, ref EmoteAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnThrowAttempt(Entity<DroneComponent> ent, ref ThrowAttemptEvent args)
    {
        args.Cancel();
    }

    private void SetStatus(EntityUid uid, DroneStatus status)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, DroneVisuals.Status, status, appearance);
    }

    private void UpdateBatteryAlert(Entity<DroneComponent> ent)
    {
        if (!HasComp<PowerCellSlotComponent>(ent.Owner)
            || !_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery)
            || battery is not { } cell)
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
            _alerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }

        var chargePercent = (short) MathF.Round(_battery.GetChargeLevel(cell.AsNullable()) * 10f);
        if (chargePercent == 5 && chargePercent < ent.Comp.LastChargePercent)
            ShowTimedPopup(ent, "drone-med-battery", PopupType.MediumCaution);
        else if (chargePercent == 2 && chargePercent < ent.Comp.LastChargePercent)
            ShowTimedPopup(ent, "drone-low-battery", PopupType.LargeCaution);

        if (chargePercent == 0 && _powerCell.HasDrawCharge(ent.Owner))
            chargePercent = 1;

        ent.Comp.LastChargePercent = chargePercent;
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargePercent);
    }

    private void ShowTimedPopup(Entity<DroneComponent> ent, string message, PopupType type)
    {
        if (_timing.CurTime < ent.Comp.NextProximityAlert)
            return;

        _popup.PopupEntity(Loc.GetString(message), ent, ent, type);
        ent.Comp.NextProximityAlert = _timing.CurTime + ent.Comp.ProximityDelay;
    }

    private void ShowProximityPopup(Entity<DroneComponent> ent, string message)
    {
        if (_timing.CurTime < ent.Comp.NextProximityAlert)
            return;

        _popup.PopupEntity(Loc.GetString(message, ("being", ent.Comp.NearestEntity)), ent, ent);
        ent.Comp.NextProximityAlert = _timing.CurTime + ent.Comp.ProximityDelay;
    }

    private bool NonDronesInRange(Entity<DroneComponent> ent)
    {
        var coordinates = _transform.GetMapCoordinates(Transform(ent));
        foreach (var entity in _lookup.GetEntitiesInRange(coordinates, ent.Comp.InteractionBlockRange))
        {
            if (!HasComp<MindContainerComponent>(entity) || HasComp<DroneComponent>(entity) || HasComp<GhostComponent>(entity))
                continue;
            if (TryComp<MobStateComponent>(entity, out var state) && _mobState.IsDead(entity, state))
                continue;

            ent.Comp.NearestEntity = Identity.Entity(entity, EntityManager);
            return true;
        }

        return false;
    }
}

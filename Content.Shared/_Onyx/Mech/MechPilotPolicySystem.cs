using Content.Shared._Onyx.Carrying;
using Content.Shared._Onyx.Sprinting;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mech.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Implements pilot policies without coupling them to the upstream mech lifecycle.
/// </summary>
public sealed partial class MechPilotPolicySystem : EntitySystem
{
    private const int RequiredHands = 2;

    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItems = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CarryingSystem _carrying = default!;
    [Dependency] private SharedSprintingSystem _sprinting = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    private readonly HashSet<EntityUid> _rollingBack = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, AttemptMechInsertEvent>(OnAttemptInsert);
        SubscribeLocalEvent<MetaDataComponent, AttemptMechEjectEvent>(OnAttemptEject);
        SubscribeLocalEvent<MetaDataComponent, MechInsertedEvent>(OnInserted);
        SubscribeLocalEvent<MetaDataComponent, MechEjectedEvent>(OnEjected);
        SubscribeLocalEvent<MechComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
    }

    private void OnAttemptInsert(Entity<MetaDataComponent> pilot, ref AttemptMechInsertEvent args)
    {
        if (args.Cancelled || !TryComp<MechComponent>(args.Mech, out var mech))
            return;

        if (mech.Energy <= 0)
        {
            _popup.PopupEntity(Loc.GetString("mech-unpowered-entry-denied"), args.Mech, pilot);
            args.Cancelled = true;
            return;
        }

        if (!TryComp<VehicleComponent>(args.Mech, out var vehicle) ||
            _whitelist.IsWhitelistFail(vehicle.OperatorWhitelist, pilot) ||
            _whitelist.IsWhitelistPass(mech.PilotBlacklist, pilot))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", args.Mech)), pilot, pilot);
            args.Cancelled = true;
            return;
        }

        if (!TryComp<HandsComponent>(pilot, out var hands))
            return;

        if (GetUsableHands((pilot, hands)).Count < RequiredHands)
            args.Cancelled = true;
    }

    private void OnAttemptEject(Entity<MetaDataComponent> pilot, ref AttemptMechEjectEvent args)
    {
        if (!args.Forced && TryComp<MechComponent>(args.Mech, out var mech) && mech.Energy <= 0)
            _popup.PopupEntity(Loc.GetString("mech-emergency-eject"), args.Mech, pilot);
    }

    private void OnInserted(Entity<MetaDataComponent> pilot, ref MechInsertedEvent args)
    {
        _sprinting.StopSprint(pilot);
        _sprinting.StopSprint(args.Mech);
        if (TryComp<HandsComponent>(pilot, out var hands) && !BlockHands(pilot, hands, args.Mech))
        {
            args.Cancelled = true;
            return;
        }

        var pilotFactions = CompOrNull<NpcFactionMemberComponent>(pilot)?.Factions ?? new();
        RelayFactions(args.Mech, pilotFactions);
    }

    private void OnEjected(Entity<MetaDataComponent> pilot, ref MechEjectedEvent args)
    {
        _sprinting.StopSprint(pilot);
        _sprinting.StopSprint(args.Mech);
        _virtualItems.DeleteInHandsMatching(pilot, args.Mech);
        RestoreFactions(args.Mech);
    }

    private void OnVirtualItemDeleted(Entity<MechComponent> mech, ref VirtualItemDeletedEvent args)
    {
        if (_rollingBack.Contains(args.User) ||
            _vehicle.GetOperatorOrNull(mech.Owner) != args.User) // <Onyx-MechVehicleNative>
            return;

        _containers.Remove(args.User, mech.Comp.PilotSlot, force: true);
    }

    private List<string> GetUsableHands(Entity<HandsComponent> pilot)
    {
        var result = new List<string>(RequiredHands);
        var nullablePilot = new Entity<HandsComponent?>(pilot.Owner, pilot.Comp);
        foreach (var hand in _hands.EnumerateHands(nullablePilot))
        {
            if (!_hands.TryGetHeldItem(nullablePilot, hand, out var held))
            {
                result.Add(hand);
            }
            else if (TryComp<VirtualItemComponent>(held, out var virtualItem))
            {
                if (TryComp<CarryingComponent>(pilot, out var carrying) &&
                    virtualItem.BlockingEntity == carrying.Carried)
                    result.Add(hand);
            }
            else if (_hands.CanDropHeld(pilot.Owner, hand, checkActionBlocker: false))
            {
                result.Add(hand);
            }

            if (result.Count == RequiredHands)
                break;
        }

        return result;
    }

    private bool BlockHands(EntityUid pilot, HandsComponent hands, EntityUid mech)
    {
        var usable = GetUsableHands((pilot, hands));
        if (usable.Count < RequiredHands)
            return false;

        if (TryComp<CarryingComponent>(pilot, out var carrying))
        {
            _carrying.DropCarried(pilot, carrying.Carried);
            usable = GetUsableHands((pilot, hands));
            if (usable.Count < RequiredHands)
                return false;
        }

        var activeHand = hands.ActiveHandId;
        var dropped = new List<(string Hand, EntityUid Item)>(RequiredHands);
        var blockers = new List<EntityUid>(RequiredHands);
        for (var i = 0; i < RequiredHands; i++)
        {
            var hand = usable[i];
            if (_hands.TryGetHeldItem((pilot, hands), hand, out var held))
            {
                dropped.Add((hand, held!.Value));
                if (!_hands.TryDrop((pilot, hands), hand, checkActionBlocker: false, doDropInteraction: false))
                {
                    RollbackHands(pilot, hands, activeHand, dropped, blockers);
                    return false;
                }
            }

            if (!_virtualItems.TrySpawnVirtualItemInHand(mech, pilot, out var item, empty: hand, silent: true) ||
                !_hands.IsHolding((pilot, hands), item.Value))
            {
                RollbackHands(pilot, hands, activeHand, dropped, blockers);
                return false;
            }

            EnsureComp<UnremoveableComponent>(item.Value);
            blockers.Add(item.Value);
        }

        return true;
    }

    private void RollbackHands(EntityUid pilot,
        HandsComponent hands,
        string? activeHand,
        List<(string Hand, EntityUid Item)> dropped,
        List<EntityUid> blockers)
    {
        _rollingBack.Add(pilot);
        try
        {
            foreach (var blocker in blockers)
            {
                if (TryComp<VirtualItemComponent>(blocker, out var virtualItem))
                    _virtualItems.DeleteVirtualItem((blocker, virtualItem), pilot);
            }

            foreach (var (hand, item) in dropped)
            {
                if (!_hands.TryGetHeldItem((pilot, hands), hand, out _))
                    _hands.TryPickup(pilot, item, hand, checkActionBlocker: false, animate: false, handsComp: hands);
            }

            _hands.TrySetActiveHand((pilot, hands), activeHand);
        }
        finally
        {
            _rollingBack.Remove(pilot);
        }
    }

    private void RelayFactions(EntityUid mech, HashSet<ProtoId<NpcFactionPrototype>> pilotFactions)
    {
        var state = EnsureComp<MechFactionRelayComponent>(mech);
        if (!TryComp<NpcFactionMemberComponent>(mech, out var mechFactions))
        {
            state.HadFactionComponent = false;
            mechFactions = EnsureComp<NpcFactionMemberComponent>(mech);
        }
        else
        {
            state.HadFactionComponent = true;
            state.OriginalFactions = new HashSet<ProtoId<NpcFactionPrototype>>(mechFactions.Factions);
        }

        _factions.ClearFactions((mech, mechFactions), dirty: false);
        _factions.AddFactions((mech, mechFactions), new HashSet<ProtoId<NpcFactionPrototype>>(pilotFactions));
    }

    private void RestoreFactions(EntityUid mech)
    {
        if (!TryComp<MechFactionRelayComponent>(mech, out var state))
            return;

        if (TryComp<NpcFactionMemberComponent>(mech, out var factions))
            _factions.ClearFactions((mech, factions), dirty: false);

        if (state.HadFactionComponent)
            _factions.AddFactions((mech, EnsureComp<NpcFactionMemberComponent>(mech)),
                new HashSet<ProtoId<NpcFactionPrototype>>(state.OriginalFactions));
        else
            RemComp<NpcFactionMemberComponent>(mech);

        RemComp<MechFactionRelayComponent>(mech);
    }
}

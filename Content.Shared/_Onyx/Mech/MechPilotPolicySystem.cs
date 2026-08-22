using Content.Shared._Onyx.Sprinting;
using Content.Shared.Mech.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Applies mech-specific admission and pilot lifecycle policies on top of the generic vehicle lifecycle.
/// </summary>
public sealed partial class MechPilotPolicySystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSprintingSystem _sprinting = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<MetaDataComponent, OnVehicleEnteredEvent>(OnEntered);
        SubscribeLocalEvent<MetaDataComponent, OnVehicleExitedEvent>(OnExited);
    }

    private void OnInsertAttempt(Entity<MechComponent> mech, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp<ContainerVehicleComponent>(mech, out var containerVehicle) ||
            args.Container.ID != containerVehicle.ContainerId)
            return;

        if (mech.Comp.Energy <= 0)
        {
            _popup.PopupEntity(Loc.GetString("mech-unpowered-entry-denied"), mech, args.EntityUid);
            args.Cancel();
            return;
        }

        if (mech.Comp.Broken ||
            !_vehicle.CanOperate(mech.Owner, args.EntityUid) ||
            _whitelist.IsWhitelistPass(mech.Comp.PilotBlacklist, args.EntityUid))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", mech.Owner)), args.EntityUid, args.EntityUid);
            args.Cancel();
        }
    }

    private void OnEntered(Entity<MetaDataComponent> pilot, ref OnVehicleEnteredEvent args)
    {
        if (!HasComp<MechComponent>(args.Vehicle))
            return;

        _sprinting.StopSprint(pilot);
        _sprinting.StopSprint(args.Vehicle);
        var pilotFactions = CompOrNull<NpcFactionMemberComponent>(pilot)?.Factions ?? new();
        RelayFactions(args.Vehicle, pilotFactions);
    }

    private void OnExited(Entity<MetaDataComponent> pilot, ref OnVehicleExitedEvent args)
    {
        if (!HasComp<MechComponent>(args.Vehicle))
            return;

        _sprinting.StopSprint(pilot);
        _sprinting.StopSprint(args.Vehicle);
        RestoreFactions(args.Vehicle);
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

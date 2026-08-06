using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Popups; // <Onyx-FTLDrive>
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    private void InitializeFTL()
    {
        SubscribeLocalEvent<FTLBeaconComponent, ComponentStartup>(OnBeaconStartup);
        SubscribeLocalEvent<FTLBeaconComponent, AnchorStateChangedEvent>(OnBeaconAnchorChanged);

        SubscribeLocalEvent<FTLExclusionComponent, ComponentStartup>(OnExclusionStartup);
    }

    private void OnExclusionStartup(Entity<FTLExclusionComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconStartup(Entity<FTLBeaconComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconAnchorChanged(Entity<FTLBeaconComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconFTLMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleFTLBeaconMessage args)
    {
        var beaconEnt = GetEntity(args.Beacon);
        if (!TryComp(beaconEnt, out TransformComponent? targetXform))
        {
            return;
        }

        var nCoordinates = new NetCoordinates(GetNetEntity(targetXform.ParentUid), targetXform.LocalPosition);
        if (targetXform.ParentUid == EntityUid.Invalid)
        {
            nCoordinates = new NetCoordinates(GetNetEntity(beaconEnt), targetXform.LocalPosition);
        }

        // Check target exists
        if (!_shuttle.CanFTLBeacon(nCoordinates))
        {
            return;
        }

        var angle = args.Angle.Reduced();
        var targetCoordinates = new EntityCoordinates(targetXform.MapUid!.Value, _transform.GetWorldPosition(targetXform));

        ConsoleFTL(ent, targetCoordinates, angle, targetXform.MapID, args.Actor); // <Onyx-FTLDrive-edited>
    }

    private void OnPositionFTLMessage(Entity<ShuttleConsoleComponent> entity, ref ShuttleConsoleFTLPositionMessage args)
    {
        var mapUid = _mapSystem.GetMap(args.Coordinates.MapId);

        // If it's beacons only block all position messages.
        if (!Exists(mapUid) || _shuttle.IsBeaconMap(mapUid))
        {
            return;
        }

        var targetCoordinates = new EntityCoordinates(mapUid, args.Coordinates.Position);
        var angle = args.Angle.Reduced();
        ConsoleFTL(entity, targetCoordinates, angle, args.Coordinates.MapId, args.Actor); // <Onyx-FTLDrive-edited>
    }

    private void GetBeacons(ref List<ShuttleBeaconObject>? beacons)
    {
        var beaconQuery = AllEntityQuery<FTLBeaconComponent>();

        while (beaconQuery.MoveNext(out var destUid, out _))
        {
            var meta = MetaData(destUid);
            var name = meta.EntityName;

            if (string.IsNullOrEmpty(name))
                name = Loc.GetString("shuttle-console-unknown");

            // Can't travel to same map (yet)
            var destXform = Transform(destUid);
            beacons ??= new List<ShuttleBeaconObject>();
            beacons.Add(new ShuttleBeaconObject(GetNetEntity(destUid), GetNetCoordinates(destXform.Coordinates), name));
        }
    }

    private void GetExclusions(ref List<ShuttleExclusionObject>? exclusions)
    {
        var query = AllEntityQuery<FTLExclusionComponent, TransformComponent>();

        while (query.MoveNext(out var comp, out var xform))
        {
            if (!comp.Enabled)
                continue;

            exclusions ??= new List<ShuttleExclusionObject>();
            exclusions.Add(new ShuttleExclusionObject(GetNetCoordinates(xform.Coordinates), comp.Range, Loc.GetString("shuttle-console-exclusion")));
        }
    }

    /// <summary>
    /// Handles shuttle console FTLs.
    /// </summary>
    private void ConsoleFTL(Entity<ShuttleConsoleComponent> ent, EntityCoordinates targetCoordinates, Angle targetAngle, MapId targetMap, EntityUid user) // <Onyx-FTLDrive-edited>
    {
        var consoleUid = GetDroneConsole(ent.Owner);

        if (consoleUid == null)
            return;

        var shuttleUid = Transform(consoleUid.Value).GridUid;

        if (shuttleUid == null)
            return;

        var selectedShuttleUid = shuttleUid.Value; // <Onyx-ZLevels>
        var actualShuttleUid = _shuttle.ResolveFTLShuttle(selectedShuttleUid); // <Onyx-ZLevels>

        if (!TryComp(actualShuttleUid, out ShuttleComponent? shuttleComp))
            return;

        if (shuttleComp.Enabled == false)
            return;

        // Check shuttle can even FTL
        if (!_shuttle.CanFTL(actualShuttleUid, out var reason)) // <Onyx-ZLevels-edited>
        {
            _popup.PopupEntity(reason, ent, user, PopupType.MediumCaution); // <Onyx-FTLDrive-edited>
            return;
        }

        // Check shuttle can FTL to this target.
        if (_ztravel.IsTraversing(actualShuttleUid)) // <Onyx-ZLevels>
            return;

        if (!TryComp(actualShuttleUid, out PhysicsComponent? shuttlePhysics))
            return;

        // <Onyx-ZLevels-edited>
        var adjustedCoordinates = targetCoordinates.Offset(targetAngle.RotateVec(-shuttlePhysics.LocalCenter));
        var actualTargetCoordinates = _shuttle.ResolveFTLTargetCoordinates(selectedShuttleUid, adjustedCoordinates);
        var actualTargetMap = _transform.GetMapId(actualTargetCoordinates);
        var selectedMap = Transform(selectedShuttleUid).MapID;
        var allowResolvedSameMap = selectedShuttleUid != actualShuttleUid && selectedMap != targetMap;

        if (!_shuttle.CanFTLTo(actualShuttleUid, actualTargetMap, ent))
        {
            return;
        }

        List<ShuttleExclusionObject>? exclusions = null;
        GetExclusions(ref exclusions);

        if (!_shuttle.FTLFree(actualShuttleUid, actualTargetCoordinates, targetAngle, exclusions, allowResolvedSameMap))
        {
            return;
        }
        // </Onyx-ZLevels-edited>

        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(actualShuttleUid, ref tagEv); // <Onyx-ZLevels-edited>

        var ev = new ShuttleConsoleFTLTravelStartEvent(ent.Owner);
        RaiseLocalEvent(ref ev);

        _shuttle.FTLToCoordinates(actualShuttleUid, shuttleComp, actualTargetCoordinates, targetAngle); // <Onyx-ZLevels-edited>
    }
}

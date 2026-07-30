using System.Linq;
using Content.Shared._Onyx.GPS;
using Content.Shared.CCVar;
using Content.Shared.Pinpointer;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._Onyx.GPS;

public sealed partial class GpsSystem : SharedGpsSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IRobustRandom _random = default!;

    private float _updateRate = 1f;
    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GPSComponent, MapInitEvent>(OnGpsInit);
        SubscribeLocalEvent<GPSComponent, BoundUIOpenedEvent>(OnUiOpened);
        Subs.CVar(_configuration, CCVars.GpsUpdateRate,
            value => _updateRate = float.IsFinite(value) && value > 0f ? value : 1f,
            true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _updateTimer += frameTime;
        if (_updateTimer < _updateRate)
            return;

        _updateTimer -= _updateRate;
        var allEntries = GetGpsEntries();
        var query = AllEntityQuery<GPSComponent, ActiveUserInterfaceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gps, out _, out var xform))
        {
            var entries = allEntries
                .Where(entry => entry.NetEntity != GetNetEntity(uid) && xform.MapID == entry.Coordinates.MapId)
                .ToList();
            UiSystem.ServerSendUiMessage(uid, GpsUiKey.Key,
                new GpsUpdateMessage(gps.GpsName, gps.TrackedEntity, gps.InDistress, gps.Enabled, entries));
        }
    }

    private void OnUiOpened(Entity<GPSComponent> ent, ref BoundUIOpenedEvent args)
    {
        var xform = Transform(ent);
        var entries = GetGpsEntries()
            .Where(entry => entry.NetEntity != GetNetEntity(ent) && xform.MapID == entry.Coordinates.MapId)
            .ToList();
        UiSystem.ServerSendUiMessage(ent.Owner, GpsUiKey.Key,
            new GpsUpdateMessage(ent.Comp.GpsName, ent.Comp.TrackedEntity, ent.Comp.InDistress, ent.Comp.Enabled, entries),
            args.Actor);
    }

    protected override bool CanTrack(Entity<GPSComponent> ent, NetEntity? trackedEntity)
    {
        if (trackedEntity == null)
            return true;

        var target = GetEntity(trackedEntity.Value);
        return Exists(target) && Transform(target).MapID == Transform(ent).MapID &&
               (TryComp<GPSComponent>(target, out var gps) && gps.Enabled ||
                TryComp<NavMapBeaconComponent>(target, out var beacon) && beacon.Enabled);
    }

    private void OnGpsInit(Entity<GPSComponent> ent, ref MapInitEvent args)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.GpsName))
            return;

        ent.Comp.GpsName = $"GPS-{_random.Next(1000, 9999)}";
        DirtyField(ent, ent.Comp, nameof(GPSComponent.GpsName));
    }

    private List<GpsEntry> GetGpsEntries()
    {
        var entries = new List<GpsEntry>();
        var gpsQuery = EntityQueryEnumerator<GPSComponent, TransformComponent>();
        while (gpsQuery.MoveNext(out var uid, out var gps, out var xform))
        {
            if (!gps.Enabled)
                continue;

            entries.Add(new GpsEntry(
                GetNetEntity(uid),
                string.IsNullOrEmpty(gps.GpsName) ? Loc.GetString("gps-entry-unknown") : gps.GpsName,
                MetaData(uid).EntityPrototype?.ID,
                gps.InDistress,
                Color.White,
                _transform.GetMapCoordinates(uid, xform)));
        }

        var beaconQuery = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (beaconQuery.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (!beacon.Enabled)
                continue;

            entries.Add(new GpsEntry(
                GetNetEntity(uid),
                beacon.Text ?? Loc.GetString("gps-entry-beacon"),
                MetaData(uid).EntityPrototype?.ID,
                false,
                beacon.Color,
                _transform.GetMapCoordinates(uid, xform)));
        }

        return entries;
    }
}

using Content.Server._Onyx.MobCaller;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.SpaceWhale;

public sealed partial class StationProximitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        _nextCheck = _timing.CurTime + CheckInterval;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;
        CheckStationProximity();
    }

    private void CheckStationProximity()
    {
        if (!_configuration.GetCVar(CCVars.SpaceWhaleSpawn))
            return;

        var stationQuery = EntityQueryEnumerator<BecomesStationComponent, MapGridComponent>();
        var stations = new List<(EntityUid Uid, MapGridComponent Grid, TransformComponent Xform)>();
        while (stationQuery.MoveNext(out var uid, out _, out var grid))
            stations.Add((uid, grid, Transform(uid)));

        if (stations.Count == 0)
            return;

        var humanoidQuery = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (humanoidQuery.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (mobState.CurrentState != MobState.Alive || !stations.Exists(station => station.Xform.MapUid == xform.MapUid))
                continue;

            CheckHumanoidProximity(uid, stations, xform);
        }
    }

    private void CheckHumanoidProximity(EntityUid humanoid,
        List<(EntityUid Uid, MapGridComponent Grid, TransformComponent Xform)> stations,
        TransformComponent humanoidXform)
    {
        var nearStation = humanoidXform.GridUid is { } gridUid && stations.Exists(station => station.Uid == gridUid);
        if (!nearStation)
        {
            var humanoidPosition = _transform.GetWorldPosition(humanoidXform);
            var closestDistance = float.MaxValue;
            foreach (var (_, grid, stationXform) in stations)
            {
                if (stationXform.MapUid != humanoidXform.MapUid)
                    continue;

                var distance = (humanoidPosition - _transform.GetWorldPosition(stationXform)).Length();
                distance = Math.Max(0, distance - grid.LocalAABB.Size.Length() / 2f);
                closestDistance = Math.Min(closestDistance, distance);
            }

            nearStation = closestDistance <= _configuration.GetCVar(CCVars.SpaceWhaleSpawnDistance);
        }

        if (!nearStation)
        {
            HandleFarFromStation(humanoid);
            return;
        }

        if (TryComp<SpaceWhaleTargetComponent>(humanoid, out var target))
        {
            QueueDel(target.Entity);
            RemComp<SpaceWhaleTargetComponent>(humanoid);
        }
    }

    private void HandleFarFromStation(EntityUid entity)
    {
        if (HasComp<SpaceWhaleTargetComponent>(entity))
            return;

        _popup.PopupEntity(Loc.GetString("station-proximity-far-from-station"), entity, entity, PopupType.LargeCaution);
        _audio.PlayEntity(new SoundPathSpecifier("/Audio/_Onyx/Ambience/SpaceWhale/leviathan-appear.ogg"),
            entity,
            entity,
            AudioParams.Default.WithVolume(1f));

        var dummy = Spawn(null, Transform(entity).Coordinates);
        _transform.SetParent(dummy, entity);
        var caller = EnsureComp<MobCallerComponent>(dummy);
        caller.SpawnProto = "SpaceLeviathanDespawn";
        caller.MaxAlive = 1;
        caller.MinDistance = 100f;
        caller.NeedAnchored = false;
        caller.NeedPower = false;
        caller.SpawnSpacing = TimeSpan.FromSeconds(65);

        EnsureComp<SpaceWhaleTargetComponent>(entity).Entity = dummy;
    }
}

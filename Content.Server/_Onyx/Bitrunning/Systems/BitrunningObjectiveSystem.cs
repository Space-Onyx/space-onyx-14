using System.Linq;
using Content.Shared._Onyx.Effects;
using Content.Shared._Onyx.Fishing.Events;
using Content.Shared._Onyx.Bitrunning;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Server.Containers;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;
namespace Content.Server._Onyx.Bitrunning.Systems;

public sealed partial class BitrunningObjectiveSystem : EntitySystem
{
    [Dependency] private QuantumServerSystem _server = default!;
    [Dependency] private ByteforgeSystem _byteforge = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SparksSystem _sparks = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BitrunningExitMarkerComponent, StartCollideEvent>(OnExitCollide);
        SubscribeLocalEvent<BitrunningObjectivePointComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<BitrunningObjectiveDeliveryPointComponent, StartCollideEvent>(OnDeliveryCollide);
        SubscribeLocalEvent<BitrunningDomainEnemyObjectiveComponent, ComponentStartup>(OnEnemyObjectiveStartup);
        SubscribeLocalEvent<BitrunningDomainEnemyObjectiveComponent, MobStateChangedEvent>(OnEnemyStateChanged);
        SubscribeLocalEvent<BitrunningDomainEnemyObjectiveComponent, EntityTerminatingEvent>(OnEnemyTerminating);
        SubscribeLocalEvent<BitrunningDespawnOnOpenComponent, StorageAfterOpenEvent>(OnRewardCacheOpened);
        SubscribeLocalEvent<AvatarConnectionComponent, FishCaughtEvent>(OnFishCaught);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var servers = EntityQueryEnumerator<QuantumServerComponent>();
        while (servers.MoveNext(out var serverUid, out var server))
        {
            if (server.State != BitrunningServerState.Running
                || server.ObjectiveCompleted
                || (server.ObjectiveType != BitrunningObjectiveType.FillStomach
                    && server.ObjectiveType != BitrunningObjectiveType.OverhydrateStomach))
                continue;

            if (_timing.CurTime < server.NextSatiationProgressTime)
                continue;

            foreach (var avatar in server.ActiveConnections)
            {
                if (!IsAvatarMeetingSatiationObjective(avatar, server.ObjectiveType))
                    continue;

                _server.AddObjectiveProgress(serverUid, 1);
                server.NextSatiationProgressTime = _timing.CurTime + TimeSpan.FromSeconds(1);
                break;
            }
        }
    }

    private void OnExitCollide(Entity<BitrunningExitMarkerComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<AvatarConnectionComponent>(args.OtherEntity))
            return;

        if (!TryResolveDomainMapUid(ent.Owner, args.OtherEntity, out var mapUid))
            return;

        if (!_server.TryGetServerByDomainMap(mapUid, out _, out _))
            return;

        _server.TryRequestDisconnectAvatar(args.OtherEntity, args.OtherEntity, true);
    }

    private void OnInteract(Entity<BitrunningObjectivePointComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryResolveDomainMapUid(ent.Owner, args.User, out var mapUid, out var coordinates))
            return;

        if (!_server.TryGetServerByDomainMap(mapUid, out var serverUid, out var server))
            return;

        if (!HasComp<AvatarConnectionComponent>(args.User))
            return;

        if (server.ObjectiveType != BitrunningObjectiveType.CollectEncryptedCaches)
            return;

        _server.AddObjectiveProgress(serverUid, ent.Comp.Points);
        _audio.PlayPvs(ent.Comp.PickupSound, coordinates);
        if (ent.Comp.ConsumeOnUse)
            QueueDel(ent.Owner);

        args.Handled = true;
    }

    private void OnDeliveryCollide(Entity<BitrunningObjectiveDeliveryPointComponent> ent, ref StartCollideEvent args)
    {
        if (!TryResolveDomainMapUid(ent.Owner, args.OtherEntity, out var mapUid))
            return;

        if (!_server.TryGetServerByDomainMap(mapUid, out var serverUid, out var server))
            return;

        if (!HasComp<BitrunningObjectiveCargoComponent>(args.OtherEntity))
            return;

        if (!TryComp<BitrunningObjectiveCargoComponent>(args.OtherEntity, out var cargo) || cargo.Server != null && cargo.Server != serverUid)
            return;

        if (HasComp<BitrunningDeliveredObjectiveCargoComponent>(args.OtherEntity))
            return;

        if (!_byteforge.HasLinkedByteforge(serverUid, server))
        {
            if (TryComp<MapComponent>(mapUid, out var mapComp))
                _popup.PopupEntity(Loc.GetString("bitrunning-delivery-byteforge-required"), ent, Filter.BroadcastMap(mapComp.MapId), true, PopupType.LargeCaution);

            return;
        }

        if (!_byteforge.TryDeliverObjectiveCargoToByteforge(serverUid, args.OtherEntity))
            return;

        _sparks.DoSparks(Transform(ent).Coordinates);

        if (server.ObjectiveType == BitrunningObjectiveType.DeliveryCacheCrate)
            _server.AddObjectiveProgress(serverUid, ent.Comp.Points);
    }

    private void OnEnemyObjectiveStartup(Entity<BitrunningDomainEnemyObjectiveComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.DomainMapUid != null)
            return;

        if (!TryComp(ent.Owner, out TransformComponent? xform) || xform.MapUid is not { } mapUid)
            return;

        ent.Comp.DomainMapUid = mapUid;
    }

    private void OnEnemyStateChanged(Entity<BitrunningDomainEnemyObjectiveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        TryAwardEnemyEliminationProgress(ent);
    }

    private void OnEnemyTerminating(Entity<BitrunningDomainEnemyObjectiveComponent> ent, ref EntityTerminatingEvent args)
    {
        TryAwardEnemyEliminationProgress(ent);
    }

    private void TryAwardEnemyEliminationProgress(Entity<BitrunningDomainEnemyObjectiveComponent> ent)
    {
        if (HasComp<BitrunningEnemyObjectiveCountedComponent>(ent))
            return;

        var resolvedMapUid = ent.Comp.DomainMapUid;
        EntityUid mapUid;

        if (resolvedMapUid is { } storedMapUid)
            mapUid = storedMapUid;
        else if (!TryResolveDomainMapUid(ent.Owner, null, out mapUid))
            return;

        if (!_server.TryGetServerByDomainMap(mapUid, out var serverUid, out var server))
            return;

        if (server.State != BitrunningServerState.Running)
            return;

        if (resolvedMapUid == null)
            ent.Comp.DomainMapUid = mapUid;

        if (server.ObjectiveType != BitrunningObjectiveType.EliminateEnemies)
            return;

        EnsureComp<BitrunningEnemyObjectiveCountedComponent>(ent);
        _server.AddObjectiveProgress(serverUid, ent.Comp.Points);
    }

    private void OnFishCaught(Entity<AvatarConnectionComponent> ent, ref FishCaughtEvent args)
    {
        if (!TryResolveDomainMapUid(ent.Owner, null, out var mapUid))
            return;

        if (!_server.TryGetServerByDomainMap(mapUid, out var serverUid, out var server))
            return;

        if (server.ObjectiveType != BitrunningObjectiveType.CatchFish)
            return;

        _server.AddObjectiveProgress(serverUid, 1);
    }

    private void OnRewardCacheOpened(Entity<BitrunningDespawnOnOpenComponent> ent, ref StorageAfterOpenEvent args)
    {
        var dropCoordinates = Transform(ent).Coordinates;
        EjectEntitiesFromStorage(ent, dropCoordinates);
        _sparks.DoSparks(dropCoordinates);
        QueueDel(ent);
    }

    private void EjectEntitiesFromStorage(EntityUid cargoUid, EntityCoordinates dropCoordinates)
    {
        if (TryComp<StorageComponent>(cargoUid, out var storage))
            _container.EmptyContainer(storage.Container, destination: dropCoordinates);

        if (!TryComp<EntityStorageComponent>(cargoUid, out var entityStorage))
            return;

        foreach (var contained in entityStorage.Contents.ContainedEntities.ToList())
        {
            _container.Remove(contained, entityStorage.Contents, destination: dropCoordinates, reparent: true);
        }
    }

    private bool TryResolveDomainMapUid(EntityUid primaryUid, EntityUid? fallbackUid, out EntityUid mapUid, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (TryComp(primaryUid, out TransformComponent? primaryXform) && primaryXform.MapUid is { } primaryMapUid)
        {
            mapUid = primaryMapUid;
            coordinates = primaryXform.Coordinates;
            return true;
        }

        if (fallbackUid != null && TryComp(fallbackUid.Value, out TransformComponent? fallbackXform) && fallbackXform.MapUid is { } fallbackMapUid)
        {
            mapUid = fallbackMapUid;
            coordinates = fallbackXform.Coordinates;
            return true;
        }

        mapUid = default;
        return false;
    }

    private bool TryResolveDomainMapUid(EntityUid primaryUid, EntityUid? fallbackUid, out EntityUid mapUid)
    {
        return TryResolveDomainMapUid(primaryUid, fallbackUid, out mapUid, out _);
    }

    private static readonly SatiationValue FillStomachAbove = "Okay";
    private static readonly SatiationValue OverhydrateAbove = "Okay";

    private bool IsAvatarMeetingSatiationObjective(EntityUid avatarUid, BitrunningObjectiveType objectiveType)
    {
        if (!TryComp<SatiationComponent>(avatarUid, out var satiation))
            return false;

        var ent = new Entity<SatiationComponent>(avatarUid, satiation);
        return objectiveType switch
        {
            BitrunningObjectiveType.FillStomach => _satiation.IsValueInRange(ent, SatiationSystem.Hunger, above: FillStomachAbove),
            BitrunningObjectiveType.OverhydrateStomach => _satiation.IsValueInRange(ent, SatiationSystem.Thirst, above: OverhydrateAbove),
            _ => false,
        };
    }
}

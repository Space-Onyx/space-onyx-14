// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2024 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 August Eymann <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Onyx.Blob.Components;
using Content.Server._Onyx.Blob.GameTicking;
using Content.Shared._Onyx.Blob.Components;
using Content.Shared._Onyx.Blob.Events;
using Content.Server.Actions;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.AlertLevel;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Blob;

public sealed partial class BlobCoreSystem : EntitySystem
{
    private static readonly EntProtoId BlobRule = "BlobRule";
    private static readonly ProtoId<AlertLevelPrototype> GreenAlert = "Green";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private MetaDataSystem _metaDataSystem = default!;
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private StoreSystem _storeSystem = default!;
    [Dependency] private BlobTileSystem _blobTile = default!;

    private EntityQuery<BlobTileComponent> _tile;
    private EntityQuery<BlobFactoryComponent> _factory;
    private EntityQuery<BlobNodeComponent> _node;

    [ValidatePrototypeId<AlertPrototype>]
    private const string BlobHealth = "BlobHealth";
    [ValidatePrototypeId<AlertPrototype>]
    private const string BlobResource = "BlobResource";
    [ValidatePrototypeId<CurrencyPrototype>]
    private const string BlobMoney = "BlobPoint";

    private readonly ReaderWriterLockSlim _pointsChange = new();
    private readonly HashSet<EntityUid> _coresBeingCleaned = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCoreComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobCoreComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BlobCoreComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<BlobCoreComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<BlobCoreComponent, BlobTransformTileActionEvent>(OnTileTransform);

        SubscribeLocalEvent<BlobCoreComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<Objectives.BlobCaptureConditionComponent, ObjectiveGetProgressEvent>(OnBlobCaptureProgress);
        SubscribeLocalEvent<Objectives.BlobCaptureConditionComponent, ObjectiveAfterAssignEvent>(OnBlobCaptureInfo);
        SubscribeLocalEvent<Objectives.BlobCaptureConditionComponent, ObjectiveAssignedEvent>(OnBlobCaptureInfoAdd);


        _tile = GetEntityQuery<BlobTileComponent>();
        _factory = GetEntityQuery<BlobFactoryComponent>();
        _node = GetEntityQuery<BlobNodeComponent>();
    }

    private const double KillCoreJobTime = 0.5;
    private readonly JobQueue _killCoreJobQueue = new(KillCoreJobTime);

    public sealed class KillBlobCore(
        BlobCoreSystem system,
        EntityUid? station,
        Entity<BlobCoreComponent> ent,
        double maxTime,
        CancellationToken cancellation = default)
        : Job<object>(maxTime, cancellation)
    {
        protected override async Task<object?> Process()
        {
            system.DestroyBlobCore(ent, station);
            return null;
        }
    }

    #region Events

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _killCoreJobQueue.Process();
    }

    private void OnStartup(EntityUid uid, BlobCoreComponent component, ComponentStartup args)
    {
        if (!_tile.TryGetComponent(uid, out var blobTileComponent))
        {
            return;
        }

        if (!_node.TryGetComponent(uid, out var nodeComponent))
        {
            return;
        }

        ConnectBlobTile((uid, blobTileComponent), (uid, component), (uid, nodeComponent));

        var store = EnsureComp<StoreComponent>(uid);
        store.CurrencyWhitelist.Add(BlobMoney);

        UpdateAllAlerts((uid, component));
        ChangeChem(uid, component.DefaultChem, component, true);

        foreach (var action in component.ActionPrototypes)
        {
            EntityUid? actionUid = null;
            _action.AddAction(uid, ref actionUid, action);

            if (actionUid != null)
                component.Actions.Add(actionUid.Value);
        }

        ChangeBlobPoint((uid, component), component.StartingMoney, store);
    }

    private void OnTerminating(EntityUid uid, BlobCoreComponent component, ref EntityTerminatingEvent args)
    {
        CreateKillBlobCoreJob((uid, component));
    }

    private void OnDestruction(EntityUid uid, BlobCoreComponent component, DestructionEventArgs args)
    {
        CreateKillBlobCoreJob((uid, component));
    }

    private void OnPlayerAttached(EntityUid uid, BlobCoreComponent component, PlayerAttachedEvent args)
    {
        var xform = Transform(uid);

        if (!HasComp<MapGridComponent>(xform.GridUid))
            return;

        if (!TerminatingOrDeleted(component.Observer))
            return;

        CreateBlobObserver(uid, args.Player.UserId, component);
    }

    private void OnDamaged(EntityUid uid, BlobCoreComponent component, DamageChangedEvent args)
    {
        UpdateAllAlerts((uid, component));
    }

    private void OnTileTransform(EntityUid uid, BlobCoreComponent blobCoreComponent, BlobTransformTileActionEvent args)
    {
        TransformSpecialTile((uid, blobCoreComponent), args);
    }

    #endregion

    #region Objective

    private void OnBlobCaptureInfoAdd(Entity<Objectives.BlobCaptureConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }
        if (!TryComp<BlobObserverComponent>(args.Mind.OwnedEntity, out var blobObserverComponent)
            || !HasComp<BlobCoreComponent>(blobObserverComponent.Core))
        {
            args.Cancelled = true;
            return;
        }

        var station = _stationSystem.GetOwningStation(blobObserverComponent.Core);
        if (station == null)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Target = CompOrNull<StationBlobConfigComponent>(station)?.StageTheEnd ?? StationBlobConfigComponent.DefaultStageEnd;
    }

    private void OnBlobCaptureInfo(EntityUid uid, Objectives.BlobCaptureConditionComponent component, ref ObjectiveAfterAssignEvent args)
    {
        _metaDataSystem.SetEntityName(uid,Loc.GetString("objective-condition-blob-capture-title"));
        _metaDataSystem.SetEntityDescription(uid,Loc.GetString("objective-condition-blob-capture-description", ("count", component.Target)));
    }

    private void OnBlobCaptureProgress(EntityUid uid, Objectives.BlobCaptureConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<BlobObserverComponent>(args.Mind.OwnedEntity, out var blobObserverComponent)
            || !TryComp<BlobCoreComponent>(blobObserverComponent.Core, out var blobCoreComponent))
        {
            args.Progress = 0;
            return;
        }

        var target = component.Target;
        args.Progress = 0;

        if (target != 0)
            args.Progress = MathF.Min((float) blobCoreComponent.BlobTiles.Count / target, 1f);
        else
            args.Progress = 1f;
    }
    #endregion

    public void UpdateAllAlerts(Entity<BlobCoreComponent> core, StoreComponent? store = null)
    {
        if (!Resolve(core, ref store))
            return;

        var component = core.Comp;

        if (component.Observer == null)
            return;

        // This one for points
        var pt = store.Balance.GetValueOrDefault(BlobMoney);
        var pointsSeverity = (short) Math.Clamp(Math.Round(pt.Float() / 10f), 0, 51);
        _alerts.ShowAlert(component.Observer.Value, BlobResource, pointsSeverity);

        // And this one for health.
        if (!TryComp<DamageableComponent>(core.Owner, out var damageComp))
            return;

        var currentHealth = component.CoreBlobTotalHealth - _damageable.GetTotalDamage((core.Owner, damageComp));
        var healthSeverity = (short) Math.Clamp(Math.Round(currentHealth.Float() / 20f), 0, 20);

        _alerts.ShowAlert(component.Observer.Value, BlobHealth, healthSeverity);
    }

    public bool CreateBlobObserver(EntityUid blobCoreUid, NetUserId userId, BlobCoreComponent? core = null)
    {
        if (!Resolve(blobCoreUid, ref core))
            return false;

        var blobRule = EntityQuery<BlobRuleComponent>().FirstOrDefault();
        if (blobRule == null)
        {
            _gameTicker.StartGameRule(BlobRule, out _);
        }

        var ev = new CreateBlobObserverEvent(userId);
        RaiseLocalEvent(blobCoreUid, ev, true);

        return !ev.Cancelled;
    }

    public bool ChangeChem(EntityUid uid, BlobChemType newChem, BlobCoreComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!component.ChemСolors.ContainsKey(newChem) || !component.ChemDamageDict.ContainsKey(newChem))
            return false;

        if (!force && newChem == component.CurrentChem)
            return false;

        component.CurrentChem = newChem;
        foreach (var blobTile in component.BlobTiles)
        {
            if (!_tile.TryGetComponent(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Color = component.ChemСolors[newChem];
            Dirty(blobTile, blobTileComponent);

            ChangeBlobEntChem(blobTile, newChem, blobTileComponent);

            if (!_factory.TryGetComponent(blobTile, out var blobFactoryComponent))
                continue;

            if (!TryComp<BlobbernautComponent>(blobFactoryComponent.Blobbernaut, out var blobbernautComponent))
                continue;

            blobbernautComponent.Color = component.ChemСolors[newChem];
            Dirty(blobFactoryComponent.Blobbernaut.Value, blobbernautComponent);

            if (TryComp<MeleeWeaponComponent>(blobFactoryComponent.Blobbernaut, out var meleeWeaponComponent))
            {
                var blobbernautDamage = new DamageSpecifier();
                foreach (var keyValuePair in component.ChemDamageDict[component.CurrentChem].DamageDict)
                {
                    blobbernautDamage.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * 0.8f);
                }
                meleeWeaponComponent.Damage = blobbernautDamage;
            }

            ChangeBlobEntChem(blobFactoryComponent.Blobbernaut.Value, newChem);
        }

        return true;
    }

    private void ChangeBlobEntChem(EntityUid uid, BlobChemType newChem, BlobTileComponent? compo = null )
    {
        // No change for reflective blobs! SPCR-2025
        if(compo is not null && compo.BlobTileType == BlobTileType.Reflective)
            return;
        if (TryComp<ExplosionResistanceComponent>(uid, out var resistance))
        {
            _explosionSystem.SetExplosionResistance(uid, 1f, true, resistance);
        }

        switch (newChem)
        {
            case BlobChemType.ExplosiveLattice:
                _damageable.SetDamageModifierSetId(uid, "ExplosiveLatticeBlob");
                resistance = EnsureComp<ExplosionResistanceComponent>(uid);
                _explosionSystem.SetExplosionResistance(uid, 0f, false, resistance);
                break;
            case BlobChemType.ElectromagneticWeb:
                _damageable.SetDamageModifierSetId(uid, "ElectromagneticWebBlob");
                break;
            default:
                _damageable.SetDamageModifierSetId(uid, "BaseBlob");
                break;
        }
    }

    /// <summary>
    /// Transforms one blob tile in another type or creates a new one from scratch.
    /// </summary>
    /// <param name="oldTileUid">Uid of the ols tile that's going to get deleted.</param>
    /// <param name="blobCore">Blob core that preformed the transformation. Make sure it isn't came from the BlobTileComponent of the target!</param>
    /// <param name="nearNode">Node will be used in ConnectBlobTile method.</param>
    /// <param name="newBlobTile">Type of a new blob tile.</param>
    /// <param name="coordinates">Coordinates of a new tile.</param>
    /// <seealso cref="ConnectBlobTile"/>
    /// <seealso cref="BlobCoreComponent"/>
    public bool TransformBlobTile(
        Entity<BlobTileComponent>? oldTileUid,
        Entity<BlobCoreComponent> blobCore,
        Entity<BlobNodeComponent>? nearNode,
        BlobTileType newBlobTile,
        EntityCoordinates coordinates)
    {
        if (oldTileUid != null)
        {
            if (oldTileUid.Value.Comp.Core?.Owner != blobCore.Owner)
                return false;
        }

        if (nearNode is { } connectedNode &&
            (!_tile.TryComp(connectedNode.Owner, out var nodeTile) || nodeTile.Core?.Owner != blobCore.Owner))
            return false;

        var blobCoreComp = blobCore.Comp;
        if (!blobCoreComp.TilePrototypes.TryGetValue(newBlobTile, out var prototype))
            return false;

        var blobTileUid = Spawn(prototype, coordinates);

        if (!_tile.TryGetComponent(blobTileUid, out var blobTileComp))
        {
            QueueDel(blobTileUid);
            return false;
        }

        if (oldTileUid != null)
            RemoveBlobTile(oldTileUid.Value, blobCore);

        ConnectBlobTile((blobTileUid, blobTileComp), blobCore, nearNode);
        ChangeBlobEntChem(blobTileUid, blobCoreComp.CurrentChem, blobTileComp);

        Dirty(blobTileUid, blobTileComp);

        return true;
    }

    /// <summary>
    /// Adds BlobTile to blob core and node, if specified.
    /// </summary>
    /// <param name="tile">Entity of the blob tile.</param>
    /// <param name="core">Entity of the blob core.</param>
    /// <param name="node">If not null, tries to connect tile to the node by checking if their BlobTileType is presented in dictionary.</param>
    public void ConnectBlobTile(
        Entity<BlobTileComponent> tile,
        Entity<BlobCoreComponent> core,
        Entity<BlobNodeComponent>? node)
    {
        var coreComp = core.Comp;
        var tileComp = tile.Comp;

        coreComp.BlobTiles.Add(tile);

        tileComp.Color = coreComp.ChemСolors[coreComp.CurrentChem];
        tileComp.Core = core;
        Dirty(tile, tileComp);

        if (node == null)
            return;

        switch (tile.Comp.BlobTileType)
        {
            case BlobTileType.Factory:
                node.Value.Comp.BlobFactory = tile;
                Dirty(node.Value);
                break;
            case BlobTileType.Resource:
                node.Value.Comp.BlobResource = tile;
                Dirty(node.Value);
                break;
        }
    }

    public bool TryGetTargetBlobTile(WorldTargetActionEvent args, out Entity<BlobTileComponent>? blobTile)
    {
        blobTile = null;

        var gridUid = _transform.GetGrid(args.Target);

        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            return false;
        }

        Entity<MapGridComponent> grid = (gridUid.Value, gridComp);

        var centerTile = _mapSystem.GetLocalTilesIntersecting(grid,
                grid,
                new Box2(args.Target.Position, args.Target.Position))
            .ToArray();

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(grid, grid, tileRef.GridIndices))
            {
                if (!_tile.TryGetComponent(ent, out var blobTileComponent))
                    continue;

                blobTile = (ent, blobTileComponent);
                return true;
            }
        }

        return false;
    }

    public bool CheckValidBlobTile(
        Entity<BlobTileComponent> tile,
        Entity<BlobNodeComponent>? node,
        bool requireNode,
        BlobTransformTileActionEvent args)
    {
        var coords = Transform(tile).Coordinates;

        var newTile = args.TileType;
        var checkTile = args.TransformFrom;
        var performer = args.Performer;

        if (tile.Comp.Core == null ||
            tile.Comp.BlobTileType == newTile ||
            tile.Comp.BlobTileType == BlobTileType.Core ||
            tile.Comp.BlobTileType != checkTile && checkTile != BlobTileType.Invalid)
        {
            _popup.PopupCoordinates(Loc.GetString("blob-target-normal-blob-invalid"), coords, performer, PopupType.Large);
            return false;
        }

        var core = tile.Comp.Core.Value;

        if (checkTile == BlobTileType.Invalid)
            return true;

        // Handle node spawn
        if (newTile == BlobTileType.Node)
        {
            if (GetNearNode(coords, core, core.Comp.NodeRadiusLimit) == null)
                return true;

            _popup.PopupCoordinates(Loc.GetString("blob-target-close-to-node"), coords, performer, PopupType.Large);
            return false;
        }

        if (!requireNode)
            return true;

        if (node == null)
        {
            _popup.PopupCoordinates(Loc.GetString("blob-target-nearby-not-node"),
                coords,
                performer,
                PopupType.Large);
            return false;
        }

        if (_blobTile.IsEmptySpecial(node.Value, newTile))
            return true;

        _popup.PopupCoordinates(Loc.GetString("blob-target-already-connected"),
            coords,
            performer,
            PopupType.Large);
        return false;
    }

    public void TransformSpecialTile(Entity<BlobCoreComponent> blobCore, BlobTransformTileActionEvent args)
    {
        if (!TryGetTargetBlobTile(args, out var blobTile) || blobTile?.Comp.Core == null)
            return;

        var coords = Transform(blobTile.Value).Coordinates;
        var tileType = args.TileType;
        var nearNode = GetNearNode(coords, blobCore);

        if (blobTile.Value.Comp.Core?.Owner != blobCore.Owner ||
            !blobCore.Comp.BlobTileCosts.TryGetValue(tileType, out var cost) ||
            !blobCore.Comp.TilePrototypes.ContainsKey(tileType) ||
            !CheckValidBlobTile(blobTile.Value, nearNode, args.RequireNode, args))
            return;

        if (!TryUseAbility(blobCore, cost, coords))
            return;

        if (!TransformBlobTile(
            blobTile,
            blobCore,
            nearNode,
            tileType,
            coords))
            ChangeBlobPoint(blobCore, cost);
    }

    public void RemoveBlobTile(Entity<BlobTileComponent> tile, Entity<BlobCoreComponent> core)
    {
        QueueDel(tile);
        core.Comp.BlobTiles.Remove(tile);
    }

    private void DestroyBlobCore(Entity<BlobCoreComponent> core, EntityUid? stationUid)
    {
        if (!_coresBeingCleaned.Add(core.Owner))
            return;

        QueueDel(core.Comp.Observer);

        foreach (var blobTile in core.Comp.BlobTiles)
        {
            if (!_tile.TryGetComponent(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Core = null;
            blobTileComponent.Color = Color.White;
            Dirty(blobTile, blobTileComponent);
        }

        var blobCoreQuery = EntityQueryEnumerator<BlobCoreComponent, MetaDataComponent, TransformComponent>();
        var stationHasAliveBlobs = false;
        while (blobCoreQuery.MoveNext(out var ent, out _, out var md, out var xform))
        {
            if (TerminatingOrDeleted(ent, md) ||
                stationUid == null ||
                _stationSystem.GetOwningStation(ent, xform) != stationUid)
                continue;

            stationHasAliveBlobs = true;
            break;
        }

        if (!stationHasAliveBlobs)
        {
            var blobRuleQuery = EntityQueryEnumerator<BlobRuleComponent, ActiveGameRuleComponent>();
            while (blobRuleQuery.MoveNext(out _, out var blobRuleComp, out _))
            {
                if (stationUid == null ||
                    !blobRuleComp.StationStages.TryGetValue(stationUid.Value, out var stage) ||
                    stage is BlobStage.TheEnd or BlobStage.Default)
                    continue;

                if(stationUid != null)
                    _alertLevelSystem.SetLevel(stationUid.Value, GreenAlert, true, true, true);

                _roundEndSystem.CancelRoundEndCountdown(forceRecall: true);
                blobRuleComp.StationStages[stationUid!.Value] = BlobStage.Default;
            }
        }

        QueueDel(core);
    }

    private void CreateKillBlobCoreJob(Entity<BlobCoreComponent> core)
    {
        var station = _stationSystem.GetOwningStation(core);
        var job = new KillBlobCore(this, station, core, KillCoreJobTime);
        _killCoreJobQueue.EnqueueJob(job);
    }

    public void RemoveTileWithReturnCost(Entity<BlobTileComponent> target, Entity<BlobCoreComponent> core)
    {
        RemoveBlobTile(target, core);

        FixedPoint2 returnCost = 0;
        var tileComp = target.Comp;

        if (target.Comp.ReturnCost)
        {
            returnCost = core.Comp.BlobTileCosts[tileComp.BlobTileType];
        }

        if (returnCost <= 0)
            return;

        ChangeBlobPoint(core, returnCost);

        if (core.Comp.Observer == null)
            return;

        _popup.PopupCoordinates(Loc.GetString("blob-get-resource", ("point", returnCost)),
            Transform(target).Coordinates,
            core.Comp.Observer.Value,
            PopupType.Large);
    }

    public bool ChangeBlobPoint(Entity<BlobCoreComponent> core, FixedPoint2 amount, StoreComponent? store = null)
    {
        if (!Resolve(core, ref store))
            return false;

        if (!_pointsChange.TryEnterWriteLock(1000))
            return false;

        try
        {
            if (!_storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2>
                {
                    { BlobMoney, amount }
                },
                core,
                store))
                return false;

            UpdateAllAlerts(core, store);
            return true;
        }
        finally
        {
            _pointsChange.ExitWriteLock();
        }
    }

    /// <summary>
    /// Writes off points for some blob core and creates popup on observer or specified coordinates.
    /// </summary>
    /// <param name="core">Blob core that is going to lose points.</param>
    /// <param name="abilityCost">Cost of the ability.</param>
    /// <param name="coordinates">If not null, coordinates for popup to appear.</param>
    /// <param name="store">StoreComponent</param>
    public bool TryUseAbility(Entity<BlobCoreComponent> core, FixedPoint2 abilityCost, EntityCoordinates? coordinates = null, StoreComponent? store = null)
    {
        if (!Resolve(core, ref store))
            return false;

        var observer = core.Comp.Observer;
        if (observer == null)
            return false;

        if (!_pointsChange.TryEnterWriteLock(1000))
            return false;

        try
        {
            var money = store.Balance.GetValueOrDefault(BlobMoney);
            if (money < abilityCost)
            {
                _popup.PopupEntity(Loc.GetString(
                        "blob-not-enough-resources",
                        ("point", abilityCost.Int() - money.Int())),
                    observer.Value,
                    observer.Value,
                    PopupType.Large);
                return false;
            }

            if (!_storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2> { { BlobMoney, -abilityCost } }, core, store))
                return false;

            UpdateAllAlerts(core, store);
        }
        finally
        {
            _pointsChange.ExitWriteLock();
        }

        coordinates ??= Transform(observer.Value).Coordinates;

        _popup.PopupCoordinates(
            Loc.GetString("blob-spent-resource", ("point", abilityCost.Int())),
            coordinates.Value,
            observer.Value,
            PopupType.LargeCaution);

        return true;
    }

    /// <summary>
    /// Gets the nearest Blob node from some EntityCoordinates.
    /// </summary>
    /// <param name="coords">The EntityCoordinates to check from.</param>
    /// <param name="radius">Radius to check from coords.</param>
    /// <returns>Nearest blob node with it's component, null if wasn't founded.</returns>
    public Entity<BlobNodeComponent>? GetNearNode(
        EntityCoordinates coords,
        Entity<BlobCoreComponent> core,
        float radius = 3f)
    {
        var gridUid = _transform.GetGrid(coords);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var nearestDistance = float.MaxValue;
        var nodeComponent = new BlobNodeComponent();
        var nearestEntityUid = EntityUid.Invalid;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(
                gridUid.Value,
                grid,
                new Box2(coords.Position + new Vector2(-radius, -radius),
                    coords.Position + new Vector2(radius, radius)),
                false)
            .ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!_node.TryComp(ent, out var nodeComp) ||
                    !_tile.TryComp(ent, out var nodeTile) ||
                    nodeTile.Core?.Owner != core.Owner)
                    continue;
                var tileCords = Transform(ent).Coordinates;
                var distance = Vector2.Distance(coords.Position, tileCords.Position);

                if (!(distance < nearestDistance))
                    continue;

                nearestDistance = distance;
                nearestEntityUid = ent;
                nodeComponent = nodeComp;
            }
        }

        return nearestDistance > radius ? null : (nearestEntityUid, nodeComponent);
    }
}

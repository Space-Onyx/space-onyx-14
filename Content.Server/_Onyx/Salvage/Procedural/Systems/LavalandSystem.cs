using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Shuttles.Systems;
using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetConfigurationManager _configuration = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    private bool _enabled;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<FixturesComponent> _fixtureQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MobStateComponent, ComponentStartup>(OnMobStartup);
        SubscribeLocalEvent<MobStateComponent, ComponentRemove>(OnMobRemove);
        SubscribeLocalEvent<MobStateComponent, GridUidChangedEvent>(OnMobGridChanged);
        Subs.CVar(_configuration, CCVars.LavalandEnabled, value => _enabled = value, true);
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _fixtureQuery = GetEntityQuery<FixturesComponent>();
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        if (!_enabled || HasLavaland())
            return;

        ProtoId<LavalandMapPrototype>? selected = null;
        foreach (var map in ev.Maps)
        {
            foreach (var planet in map.Planets)
            {
                if (!_prototypes.HasIndex(planet))
                    continue;

                if (selected != null && selected != planet)
                    throw new InvalidOperationException("A station round cannot request multiple Lavaland planets.");
                selected = planet;
            }
        }

        if (selected == null)
            return;

        var preloader = CreatePreloader();
        try
        {
            if (!SetupLavalandPlanet(selected.Value, preloader, out _))
                throw new InvalidOperationException($"Failed to create required Lavaland planet '{selected}'.");
            _map.DeleteMap(Transform(preloader).MapID);
        }
        catch
        {
            DeleteAllLavalands();
            if (!TerminatingOrDeleted(preloader))
                _map.DeleteMap(Transform(preloader).MapID);
            throw;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        DeleteAllLavalands();
        var query = EntityQueryEnumerator<LavalandPreloaderComponent>();
        while (query.MoveNext(out var uid, out _))
            _map.DeleteMap(Transform(uid).MapID);
    }

    private void OnMobStartup(Entity<MobStateComponent> ent, ref ComponentStartup args)
    {
        _metadata.AddFlag(ent.Owner, MetaDataFlags.ExtraTransformEvents);
        UpdateGridGrant(ent.Owner, Transform(ent).GridUid);
    }

    private void OnMobRemove(Entity<MobStateComponent> ent, ref ComponentRemove args)
    {
        _metadata.RemoveFlag(ent.Owner, MetaDataFlags.ExtraTransformEvents);
        if (TryComp<LavalandGridGrantOwnershipComponent>(ent.Owner, out var ownership))
            EntityManager.RemoveComponents(ent.Owner, ownership.Components);
        RemComp<LavalandGridGrantOwnershipComponent>(ent.Owner);
    }

    private void OnMobGridChanged(Entity<MobStateComponent> ent, ref GridUidChangedEvent args)
    {
        if (!TerminatingOrDeleted(ent.Owner))
            UpdateGridGrant(ent.Owner, args.NewGrid);
    }

    private void UpdateGridGrant(EntityUid uid, EntityUid? newGrid)
    {
        if (TryComp<LavalandGridGrantOwnershipComponent>(uid, out var ownership))
        {
            EntityManager.RemoveComponents(uid, ownership.Components);
            RemComp<LavalandGridGrantOwnershipComponent>(uid);
        }

        if (!TryComp<LavalandGridGrantComponent>(newGrid, out var grant))
            return;

        ownership = EnsureComp<LavalandGridGrantOwnershipComponent>(uid);
        foreach (var (name, entry) in grant.ComponentsToGrant)
        {
            if (!HasComp(uid, entry.Component.GetType()))
                ownership.Components[name] = entry;
        }
        EntityManager.AddComponents(uid, ownership.Components, removeExisting: false);
    }

    private Entity<LavalandPreloaderComponent> CreatePreloader()
    {
        var uid = _map.CreateMap(out var mapId, runMapInit: false);
        var component = EnsureComp<LavalandPreloaderComponent>(uid);
        _metadata.SetEntityName(uid, "Lavaland Preloader Map");
        _map.SetPaused(mapId, true);
        return (uid, component);
    }

    private bool HasLavaland()
    {
        var query = EntityQueryEnumerator<LavalandPlanetComponent>();
        return query.MoveNext(out _, out _);
    }

    private void DeleteAllLavalands()
    {
        var query = EntityQueryEnumerator<LavalandPlanetComponent>();
        while (query.MoveNext(out var uid, out _))
            Del(uid);
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server.Procedural;
using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandSystem
{
    private readonly List<(Vector2i, Tile)> _tiles = new();
    private readonly Dictionary<ProtoId<LavalandGridRuinPrototype>, Box2> _ruinBounds = new();
    private readonly Queue<StagedTerrainEntity> _stagedTerrain = new();
    private readonly Queue<EntityUid> _stagedTerrainAllocated = new();
    private readonly HashSet<string> _stagingFallbackLogged = new();

    // ponytail: Only transform-only grid children are staged; add prototypes after verifying they have no serialized links.
    private static readonly HashSet<string> StagedTerrainPrototypes = new()
    {
        "BasaltRandom",
        "FloorLavaEntity",
        "WallRock",
        "WallRockCoal",
        "WallRockPlasma",
        "WallRockQuartz",
        "WallRockUranium",
        "WallRockChromite",
    };

    private static readonly Dictionary<string, HashSet<string>> StagedTerrainComponents = new()
    {
        ["BasaltRandom"] = new()
        {
            "Clickable", "MetaData", "RandomSprite", "RequiresTile", "Sprite", "SyncSprite", "Tag", "Transform",
        },
        ["FloorLavaEntity"] = new()
        {
            "Clickable", "CosmicCorruptible", "FishingSpot", "Fixtures", "Icon", "IconSmooth", "MetaData",
            "Physics", "Sprite", "StepTrigger", "SyncSprite", "Tag", "TileEmission", "TileEntityEffect", "Transform",
        },
        ["WallRock"] = WallRockComponents(oreVein: false),
        ["WallRockCoal"] = WallRockComponents(oreVein: true),
        ["WallRockPlasma"] = WallRockComponents(oreVein: true),
        ["WallRockQuartz"] = WallRockComponents(oreVein: true),
        ["WallRockUranium"] = WallRockComponents(oreVein: true),
        ["WallRockChromite"] = WallRockComponents(oreVein: false),
    };

    private readonly record struct StagedTerrainEntity(
        string Prototype,
        EntityUid Grid,
        Vector2 Position,
        Angle Rotation);

    private void PrepareRuins(
        LavalandRuinPoolPrototype pool,
        Entity<LavalandPlanetComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader)
    {
        var random = new Random(lavaland.Comp.Seed);
        lavaland.Comp.Preloader = preloader;
        lavaland.Comp.UsedSpace = GetLayoutBounds(lavaland);
        lavaland.Comp.RuinCoordinates = GetCoordinates(pool.RuinDistance, pool.MaxDistance);
        Shuffle(lavaland.Comp.RuinCoordinates, random);
        lavaland.Comp.GridRuins = new Queue<LavalandGridRuinPrototype>(
            Expand(pool.GridRuins).OrderBy(prototype => prototype.Priority));
        lavaland.Comp.DungeonRuins = new Queue<LavalandDungeonRuinPrototype>(
            Expand(pool.DungeonRuins).OrderBy(prototype => prototype.Priority));
        lavaland.Comp.MarkerRuins = new Queue<LavalandMarkerRuinPrototype>(
            Expand(pool.MarkerRuins).OrderBy(prototype => prototype.Priority));
        lavaland.Comp.GenerationStage = LavalandGenerationStage.GridRuins;
    }

    private bool ProcessNextRuin(Entity<LavalandPlanetComponent> lavaland)
    {
        switch (lavaland.Comp.GenerationStage)
        {
            case LavalandGenerationStage.GridRuins:
                if (lavaland.Comp.GridRuins.TryDequeue(out var gridRuin))
                {
                    LoadGridRuin(gridRuin,
                        lavaland,
                        (lavaland.Comp.Preloader, Comp<LavalandPreloaderComponent>(lavaland.Comp.Preloader)),
                        lavaland.Comp.RuinCoordinates,
                        lavaland.Comp.UsedSpace);
                    return true;
                }
                RemoveOccupiedCoordinates(lavaland.Comp.RuinCoordinates, lavaland.Comp.UsedSpace);
                lavaland.Comp.GenerationStage = LavalandGenerationStage.DungeonRuins;
                return true;
            case LavalandGenerationStage.DungeonRuins:
                if (lavaland.Comp.DungeonRuins.TryDequeue(out var dungeonRuin))
                {
                    LoadDungeonRuin(dungeonRuin,
                        lavaland,
                        lavaland.Comp.RuinCoordinates,
                        lavaland.Comp.UsedSpace);
                    return true;
                }
                RemoveOccupiedCoordinates(lavaland.Comp.RuinCoordinates, lavaland.Comp.UsedSpace);
                lavaland.Comp.GenerationStage = LavalandGenerationStage.MarkerRuins;
                return true;
            case LavalandGenerationStage.MarkerRuins:
                if (lavaland.Comp.MarkerRuins.TryDequeue(out var markerRuin))
                {
                    LoadMarkerRuin(markerRuin,
                        lavaland,
                        lavaland.Comp.RuinCoordinates,
                        lavaland.Comp.UsedSpace);
                    return true;
                }
                lavaland.Comp.GenerationStage = LavalandGenerationStage.Initializing;
                return true;
            default:
                return false;
        }
    }

    private List<Box2> GetLayoutBounds(Entity<LavalandPlanetComponent> lavaland)
    {
        var bounds = new List<Box2>();
        foreach (var uid in lavaland.Comp.LayoutGrids)
        {
            if (!_transformQuery.TryComp(uid, out var transform) ||
                !_fixtureQuery.TryComp(uid, out var fixtures) ||
                transform.MapUid != lavaland.Owner)
                continue;

            Box2? combined = null;
            var relative = _physics.GetRelativePhysicsTransform((uid, transform), lavaland.Owner);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;
                var fixtureBounds = fixture.Shape.ComputeAABB(relative, 0);
                combined = combined?.Union(fixtureBounds) ?? fixtureBounds;
            }

            if (combined is { } occupied)
                bounds.Add(occupied.Enlarged(8f));
        }
        return bounds;
    }

    private void LoadGridRuin(
        LavalandGridRuinPrototype ruin,
        Entity<LavalandPlanetComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        List<Vector2i> coordinates,
        List<Box2> usedSpace)
    {
        if (_ruinBounds.TryGetValue(ruin.ID, out var knownBounds) &&
            !TryPlaceRuin(knownBounds, ruin.SpawnAttempts, coordinates, usedSpace, out _))
        {
            Log.Warning($"No valid placement found for Lavaland grid ruin '{ruin.ID}'.");
            return;
        }

        if (!_mapLoader.TryReadFile(ruin.Path, out var data))
        {
            Log.Error($"Failed to read Lavaland grid ruin '{ruin.ID}' from '{ruin.Path}'.");
            return;
        }

        var stagedTerrain = ExtractStagedTerrain(data);
        if (stagedTerrain.Count > 0)
            Log.Debug($"Staging {stagedTerrain.Count} terrain entities from Lavaland ruin '{ruin.ID}'.");
        var options = new MapLoadOptions
        {
            MergeMap = Transform(preloader).MapID,
            ExpectedCategory = FileCategory.Grid,
        };
        if (!_mapLoader.TryLoadGeneric(data, ruin.Path.ToString(), out var result, options))
        {
            Log.Error($"Failed to preload Lavaland grid ruin '{ruin.ID}' from '{ruin.Path}'.");
            return;
        }
        if (result.Grids.Count != 1)
        {
            Log.Error($"Lavaland grid ruin '{ruin.ID}' must contain exactly one grid.");
            _mapLoader.Delete(result);
            return;
        }

        var grid = result.Grids.First();
        var placementBounds = GetPlacementBounds(grid.Comp.LocalAABB, stagedTerrain);
        _ruinBounds[ruin.ID] = placementBounds;
        if (!TryPlaceRuin(placementBounds, ruin.SpawnAttempts, coordinates, usedSpace, out var position))
        {
            Log.Warning($"No valid placement found for Lavaland grid ruin '{ruin.ID}'.");
            Del(grid.Owner);
            return;
        }

        var worldBounds = placementBounds.Translated(position.Value);
        usedSpace.Add(worldBounds);
        coordinates.Remove(position.Value);
        _transform.SetCoordinates(grid.Owner, new EntityCoordinates(preloader, position.Value));

        if (stagedTerrain.Count > 0)
        {
            var restoreGrid = grid;
            var restoreOffset = Vector2i.Zero;
            if (ruin.PatchToPlanet)
            {
                restoreGrid = (lavaland.Owner, _gridQuery.Comp(lavaland.Owner));
                restoreOffset = position.Value;
                PatchToPlanet(grid, restoreGrid, position.Value);
            }
            else
            {
                FinishGridRuin(ruin, lavaland, grid, position.Value);
            }

            foreach (var terrain in stagedTerrain)
                _stagedTerrain.Enqueue(terrain with
                {
                    Grid = restoreGrid.Owner,
                    Position = terrain.Position + restoreOffset,
                });
            return;
        }

        FinishGridRuin(ruin, lavaland, grid, position.Value);
    }

    private bool RestoreStagedTerrain()
    {
        var stopwatch = Stopwatch.StartNew();
        while (_stagedTerrain.Count > 0 && stopwatch.Elapsed < TimeSpan.FromMilliseconds(4))
        {
            var terrain = _stagedTerrain.Dequeue();
            var uid = EntityManager.CreateEntityUninitialized(
                terrain.Prototype,
                new EntityCoordinates(terrain.Grid, terrain.Position),
                rotation: terrain.Rotation);
            _stagedTerrainAllocated.Enqueue(uid);
        }

        while (_stagedTerrain.Count == 0 &&
               _stagedTerrainAllocated.Count > 0 &&
               stopwatch.Elapsed < TimeSpan.FromMilliseconds(4))
            EntityManager.InitializeAndStartEntity(_stagedTerrainAllocated.Dequeue());

        return _stagedTerrain.Count == 0 && _stagedTerrainAllocated.Count == 0;
    }

    private static Box2 GetPlacementBounds(Box2 gridBounds, List<StagedTerrainEntity> terrain)
    {
        var bounds = gridBounds;
        foreach (var entity in terrain)
            bounds = bounds.Union(Box2.FromDimensions(entity.Position - new Vector2(0.5f), Vector2.One));
        return bounds;
    }

    private void ClearStagedTerrain()
    {
        _stagedTerrain.Clear();
        while (_stagedTerrainAllocated.TryDequeue(out var uid))
        {
            if (!TerminatingOrDeleted(uid))
                Del(uid);
        }
    }

    private void FinishGridRuin(
        LavalandGridRuinPrototype ruin,
        Entity<LavalandPlanetComponent> lavaland,
        Entity<MapGridComponent> grid,
        Vector2i position)
    {

        if (ruin.PatchToPlanet)
        {
            PatchToPlanet(grid, (lavaland.Owner, _gridQuery.Comp(lavaland.Owner)), position);
            return;
        }

        _metadata.SetEntityName(grid.Owner, Loc.GetString(ruin.Name));
        _transform.SetCoordinates(grid.Owner, new EntityCoordinates(lavaland, position));
        var grant = EnsureComp<LavalandGridGrantComponent>(grid.Owner);
        foreach (var (name, component) in ruin.ComponentsToGrant)
            grant.ComponentsToGrant[name] = component;
    }

    private List<StagedTerrainEntity> ExtractStagedTerrain(MappingDataNode data)
    {
        var staged = new List<StagedTerrainEntity>();
        var groups = data.Get<SequenceDataNode>("entities");
        var scalarCounts = new Dictionary<string, int>();
        CountScalars(data, scalarCounts);
        var groupsToRemove = new List<int>();

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            if (groups[groupIndex] is not MappingDataNode group ||
                !group.TryGet<ValueDataNode>("proto", out var prototype))
                continue;

            if (!_mapMigration.TryMigrateEntityPrototype(prototype.Value, out var migrated))
                continue;

            prototype.Value = migrated;
            if (!StagedTerrainPrototypes.Contains(prototype.Value) ||
                !HasExpectedComponents(prototype.Value) ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
                continue;

            var extracted = new List<StagedTerrainEntity>(entities.Count);
            foreach (var node in entities)
            {
                if (node is not MappingDataNode entity ||
                    !TryExtractTerrainEntity(prototype.Value, entity, scalarCounts, out var terrain))
                {
                    extracted.Clear();
                    break;
                }
                extracted.Add(terrain);
            }

            if (extracted.Count == 0)
                continue;

            foreach (var terrain in extracted)
                staged.Add(terrain);
            groupsToRemove.Add(groupIndex);
        }

        for (var i = groupsToRemove.Count - 1; i >= 0; i--)
            groups.RemoveAt(groupsToRemove[i]);

        return staged;
    }

    private bool HasExpectedComponents(string prototype)
    {
        var actual = _prototypes.Index<EntityPrototype>(prototype).Components.Keys;
        if (StagedTerrainComponents[prototype].SetEquals(actual))
            return true;

        if (_stagingFallbackLogged.Add(prototype))
            Log.Warning($"Lavaland terrain staging disabled for changed prototype '{prototype}'. Using the standard map loader.");
        return false;
    }

    private static bool TryExtractTerrainEntity(
        string prototype,
        MappingDataNode entity,
        Dictionary<string, int> scalarCounts,
        out StagedTerrainEntity terrain)
    {
        terrain = default;
        if (entity.Count != 2 ||
            !entity.TryGet<ValueDataNode>("uid", out var uid) ||
            !scalarCounts.TryGetValue(uid.Value, out var references) ||
            references != 1 ||
            !entity.TryGet<SequenceDataNode>("components", out var components) ||
            components.Count != 1 ||
            components[0] is not MappingDataNode transform ||
            !transform.TryGet<ValueDataNode>("type", out var type) ||
            type.Value != "Transform" ||
            !transform.TryGet<ValueDataNode>("pos", out var position) ||
            !transform.TryGet<ValueDataNode>("parent", out var parent) ||
            parent.Value != "1" ||
            transform.Count != (transform.Has("rot") ? 4 : 3) ||
            !TryParseVector(position.Value, out var parsedPosition))
            return false;

        var rotation = Angle.Zero;
        if (transform.TryGet<ValueDataNode>("rot", out var rotationNode))
        {
            var value = rotationNode.Value;
            if (!value.EndsWith(" rad", StringComparison.Ordinal) ||
                !double.TryParse(value[..^4], NumberStyles.Float, CultureInfo.InvariantCulture, out var radians))
                return false;
            rotation = new Angle(radians);
        }

        terrain = new StagedTerrainEntity(prototype, EntityUid.Invalid, parsedPosition, rotation);
        return true;
    }

    private static void CountScalars(DataNode node, Dictionary<string, int> counts)
    {
        switch (node)
        {
            case ValueDataNode value:
                counts[value.Value] = counts.GetValueOrDefault(value.Value) + 1;
                break;
            case MappingDataNode mapping:
                foreach (var child in mapping.Values)
                    CountScalars(child, counts);
                break;
            case SequenceDataNode sequence:
                foreach (var child in sequence.Sequence)
                    CountScalars(child, counts);
                break;
        }
    }

    private static HashSet<string> WallRockComponents(bool oreVein)
    {
        var components = new HashSet<string>
        {
            "Airtight", "Anchorable", "BlockWeather", "Clickable", "Damageable", "Destructible", "Fixtures",
            "Gatherable", "GravityAffected", "Icon", "IconSmooth", "Injurable", "IsRoof", "MetaData",
            "MiningScannerViewable", "Occluder", "Physics", "PlacementReplacement", "Pullable", "RadiationBlocker",
            "RangedDamageSound", "Rotatable", "SmoothEdge", "SoundOnGather", "Sprite", "StaticPrice", "SunShadowCast",
            "Tag", "Transform", "Wall",
        };
        if (oreVein)
            components.Add("OreVein");
        return components;
    }

    private static bool TryParseVector(string value, out Vector2 vector)
    {
        vector = default;
        var separator = value.IndexOf(',');
        return separator > 0 &&
               float.TryParse(value.AsSpan(0, separator), NumberStyles.Float, CultureInfo.InvariantCulture, out vector.X) &&
               float.TryParse(value.AsSpan(separator + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out vector.Y);
    }

    private void LoadDungeonRuin(
        LavalandDungeonRuinPrototype ruin,
        Entity<LavalandPlanetComponent> lavaland,
        List<Vector2i> coordinates,
        List<Box2> usedSpace)
    {
        var localBounds = Box2.CentredAroundZero(ruin.Boundary);
        if (!TryPlaceRuin(localBounds, ruin.SpawnAttempts, coordinates, usedSpace, out var position))
        {
            Log.Warning($"No valid placement found for Lavaland dungeon ruin '{ruin.ID}'.");
            return;
        }

        usedSpace.Add(localBounds.Translated(position.Value));
        coordinates.Remove(position.Value);
        _dungeon.GenerateDungeon(
            _prototypes.Index(ruin.Config),
            lavaland.Owner,
            _gridQuery.Comp(lavaland.Owner),
            position.Value,
            lavaland.Comp.Seed);
    }

    private void LoadMarkerRuin(
        LavalandMarkerRuinPrototype ruin,
        Entity<LavalandPlanetComponent> lavaland,
        List<Vector2i> coordinates,
        List<Box2> usedSpace)
    {
        var localBounds = Box2.CentredAroundZero(ruin.Boundary);
        if (!TryPlaceRuin(localBounds, ruin.SpawnAttempts, coordinates, usedSpace, out var position))
        {
            Log.Warning($"No valid placement found for Lavaland marker ruin '{ruin.ID}'.");
            return;
        }

        usedSpace.Add(localBounds.Translated(position.Value));
        coordinates.Remove(position.Value);

        var marker = _prototypes.Index<EntityPrototype>(ruin.SpawnedMarker);
        if (!marker.Components.TryGetValue(Factory.GetComponentName<RoomFillComponent>(), out var entry) ||
            entry.Component is not RoomFillComponent roomFill)
            throw new InvalidOperationException($"Lavaland marker ruin '{ruin.ID}' must spawn an entity with RoomFill.");

        var room = _dungeon.GetRoomPrototype(_random,
            roomFill.RoomWhitelist,
            roomFill.MinSize,
            roomFill.MaxSize);
        if (room == null)
        {
            Log.Error($"Unable to find matching room prototype for Lavaland marker ruin '{ruin.ID}'.");
            return;
        }

        _dungeon.SpawnRoom(
            lavaland.Owner,
            _gridQuery.Comp(lavaland.Owner),
            position.Value - new Vector2i(room.Size.X / 2, room.Size.Y / 2),
            room,
            _random,
            null,
            clearExisting: roomFill.ClearExisting,
            rotation: roomFill.Rotation);
    }

    private static bool TryPlaceRuin(
        Box2 localBounds,
        int attempts,
        List<Vector2i> coordinates,
        List<Box2> usedSpace,
        [NotNullWhen(true)] out Vector2i? position)
    {
        for (var i = 0; i < coordinates.Count && i < attempts; i++)
        {
            var candidate = coordinates[i];
            var worldBounds = localBounds.Translated(candidate);
            if (usedSpace.Any(bounds => worldBounds.Intersects(bounds)))
                continue;
            position = candidate;
            return true;
        }

        position = null;
        return false;
    }

    private static void RemoveOccupiedCoordinates(List<Vector2i> coordinates, List<Box2> usedSpace)
    {
        coordinates.RemoveAll(position => usedSpace.Any(bounds => bounds.Contains(position)));
    }

    private static List<Vector2i> GetCoordinates(int distance, int maximum)
    {
        if (distance <= 0 || maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));

        var coordinates = new List<Vector2i>();
        for (var y = -maximum; y <= maximum; y += distance)
        for (var x = -maximum; x <= maximum; x += distance)
            coordinates.Add(new Vector2i(x, y));
        return coordinates;
    }

    private static void Shuffle<T>(List<T> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var other = random.Next(i + 1);
            (values[i], values[other]) = (values[other], values[i]);
        }
    }

    private IEnumerable<T> Expand<T>(Dictionary<ProtoId<T>, ushort> entries) where T : class, IPrototype
    {
        foreach (var (id, count) in entries)
        for (var i = 0; i < count; i++)
            yield return _prototypes.Index(id);
    }

    private void PatchToPlanet(
        Entity<MapGridComponent> source,
        Entity<MapGridComponent> destination,
        Vector2i offset)
    {
        _tiles.Clear();
        foreach (var tile in _map.GetAllTiles(source.Owner, source.Comp))
            _tiles.Add((tile.GridIndices + offset, tile.Tile));
        _map.SetTiles(destination.Owner, destination.Comp, _tiles);

        var entities = new HashSet<Entity<TransformComponent>>();
        _lookup.GetChildEntities(source, entities);
        foreach (var entity in entities)
        {
            var anchored = entity.Comp.Anchored;
            _transform.SetCoordinates(entity.Owner,
                entity.Comp,
                new EntityCoordinates(destination, entity.Comp.LocalPosition + offset));
            if (anchored)
                _transform.AnchorEntity(entity.Owner);
        }

        if (TryComp<DecalGridComponent>(source.Owner, out var decals))
        {
            EnsureComp<DecalGridComponent>(destination.Owner);
            foreach (var (_, decal) in _decals.GetDecalsIntersecting(source.Owner, source.Comp.LocalAABB))
            {
                _decals.TryAddDecal(
                    decal.Id,
                    new EntityCoordinates(destination, decal.Coordinates + offset),
                    out _,
                    decal.Color,
                    decal.Angle,
                    decal.ZIndex,
                    decal.Cleanable);
            }
        }

        Del(source.Owner);
    }
}

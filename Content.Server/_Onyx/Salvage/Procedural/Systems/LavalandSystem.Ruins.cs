using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server.Procedural;
using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandSystem
{
    private readonly List<(Vector2i, Tile)> _tiles = new();
    private readonly Dictionary<ProtoId<LavalandGridRuinPrototype>, Box2> _ruinBounds = new();

    private void SetupRuins(
        LavalandRuinPoolPrototype pool,
        Entity<LavalandPlanetComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader)
    {
        var random = new Random(lavaland.Comp.Seed);
        var usedSpace = GetLayoutBounds(lavaland);
        var coordinates = GetCoordinates(pool.RuinDistance, pool.MaxDistance);
        Shuffle(coordinates, random);

        foreach (var ruin in Expand(pool.GridRuins).OrderBy(prototype => prototype.Priority))
            LoadGridRuin(ruin, lavaland, preloader, coordinates, usedSpace);
        RemoveOccupiedCoordinates(coordinates, usedSpace);

        foreach (var ruin in Expand(pool.DungeonRuins).OrderBy(prototype => prototype.Priority))
            LoadDungeonRuin(ruin, lavaland, coordinates, usedSpace);
        RemoveOccupiedCoordinates(coordinates, usedSpace);

        foreach (var ruin in Expand(pool.MarkerRuins).OrderBy(prototype => prototype.Priority))
            LoadMarkerRuin(ruin, lavaland, coordinates, usedSpace);
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

        if (!_mapLoader.TryLoadGrid(Transform(preloader).MapID, ruin.Path, out var loaded))
        {
            Log.Error($"Failed to preload Lavaland grid ruin '{ruin.ID}' from '{ruin.Path}'.");
            return;
        }

        var grid = loaded.Value;
        _ruinBounds[ruin.ID] = grid.Comp.LocalAABB;
        if (!TryPlaceRuin(grid.Comp.LocalAABB, ruin.SpawnAttempts, coordinates, usedSpace, out var position))
        {
            Log.Warning($"No valid placement found for Lavaland grid ruin '{ruin.ID}'.");
            Del(grid.Owner);
            return;
        }

        var worldBounds = grid.Comp.LocalAABB.Translated(position.Value);
        usedSpace.Add(worldBounds);
        coordinates.Remove(position.Value);
        _transform.SetCoordinates(grid.Owner, new EntityCoordinates(preloader, position.Value));

        if (ruin.PatchToPlanet)
        {
            PatchToPlanet(grid, (lavaland.Owner, _gridQuery.Comp(lavaland.Owner)), position.Value);
            return;
        }

        _metadata.SetEntityName(grid.Owner, Loc.GetString(ruin.Name));
        _transform.SetCoordinates(grid.Owner, new EntityCoordinates(lavaland, position.Value));
        var grant = EnsureComp<LavalandGridGrantComponent>(grid.Owner);
        foreach (var (name, component) in ruin.ComponentsToGrant)
            grant.ComponentsToGrant[name] = component;
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
        Spawn(ruin.SpawnedMarker, new EntityCoordinates(lavaland, position.Value));
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
            foreach (var (_, decal) in _decals.GetDecalsIntersecting(source.Owner, source.Comp.LocalAABB, decals))
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

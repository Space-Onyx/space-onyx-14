using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Light;

public sealed partial class SunShadowOverlay
{
    private void DrawRoofShadows(
        DrawingHandleWorld worldHandle,
        Matrix3x2 inverseRenderMatrix,
        Entity<MapGridComponent> grid,
        Box2Rotated worldBounds,
        Vector2 sunDirection,
        SunShadowComponent sun)
    {
        if (!sun.CastRoofShadows)
            return;

        var hasImplicitRoof = _entManager.HasComponent<ImplicitRoofComponent>(grid.Owner);
        var hasRoof = _entManager.TryGetComponent(grid.Owner, out RoofComponent? roof);

        if (!hasImplicitRoof && !hasRoof)
            return;

        var height = float.IsFinite(sun.RoofHeight)
            ? MathF.Max(0f, sun.RoofHeight)
            : 1f;
        var worldOffset = sunDirection * height;
        var gridRotation = _xformSys.GetWorldRotation(grid.Owner);
        var localOffset = (-gridRotation).RotateVec(worldOffset);

        var gridMatrix = _xformSys.GetWorldMatrix(grid.Owner);
        worldHandle.SetTransform(Matrix3x2.Multiply(gridMatrix, inverseRenderMatrix));

        var sourceBounds = worldBounds.Enlarged(worldOffset.Length() + 0.01f);
        var tiles = _mapSys.GetTilesEnumerator(grid.Owner, grid.Comp, sourceBounds);

        while (tiles.MoveNext(out var tile))
        {
            if (!hasImplicitRoof &&
                (roof == null || _roof.GetColor((grid.Owner, grid.Comp, roof), tile.GridIndices) == null))
            {
                continue;
            }

            var localBounds = _lookup
                .GetLocalBounds(tile, grid.Comp.TileSize)
                .Translated(localOffset);

            worldHandle.DrawRect(localBounds, Color.White);
        }
    }
}

public sealed partial class RoofOverlay
{
    [Dependency] private IGameTiming _gameTiming = default!;

    private static readonly Vector2i[] CardinalDirections =
    {
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0),
    };

    private readonly Dictionary<EntityUid, EnclosedTileCache> _enclosedTileCaches = new();
    private readonly HashSet<Entity<OccluderComponent>> _occluders = new();

    private bool UsesProjectedRoofShadows(EntityUid gridUid, EntityUid mapUid)
    {
        if (_entManager.TryGetComponent(gridUid, out SunShadowComponent? gridSun))
            return gridSun.CastRoofShadows;

        return _entManager.TryGetComponent(mapUid, out SunShadowComponent? mapSun) &&
               mapSun.CastRoofShadows;
    }

    private bool IsEnclosed(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_enclosedTileCaches.TryGetValue(grid.Owner, out var cache) ||
            cache.Expires <= _gameTiming.CurTime)
        {
            cache = BuildEnclosedTileCache(grid);
            _enclosedTileCaches[grid.Owner] = cache;
        }

        return cache.Tiles.Contains(tile);
    }

    private EnclosedTileCache BuildEnclosedTileCache(Entity<MapGridComponent> grid)
    {
        var floorTiles = new HashSet<Vector2i>();
        var tiles = _mapSystem.GetAllTiles(grid.Owner, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            if (tile is { } tileRef)
                floorTiles.Add(tileRef.GridIndices);
        }

        var blockedTiles = new HashSet<Vector2i>();
        _occluders.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, grid.Comp.LocalAABB, _occluders);

        foreach (var occluder in _occluders)
        {
            if (!occluder.Comp.Enabled)
                continue;

            var xform = _entManager.GetComponent<TransformComponent>(occluder.Owner);
            blockedTiles.Add(_mapSystem.TileIndicesFor(grid, xform.Coordinates));
        }

        return BuildEnclosedTileCache(
            floorTiles,
            blockedTiles,
            _gameTiming.CurTime + TimeSpan.FromSeconds(0.5));
    }

    private static EnclosedTileCache BuildEnclosedTileCache(
        HashSet<Vector2i> floorTiles,
        HashSet<Vector2i> blockedTiles,
        TimeSpan expires = default)
    {
        var exterior = new HashSet<Vector2i>();
        var frontier = new Queue<Vector2i>();

        foreach (var tile in floorTiles)
        {
            if (blockedTiles.Contains(tile))
                continue;

            foreach (var offset in CardinalDirections)
            {
                if (floorTiles.Contains(tile + offset))
                    continue;

                exterior.Add(tile);
                frontier.Enqueue(tile);
                break;
            }
        }

        while (frontier.TryDequeue(out var tile))
        {
            foreach (var offset in CardinalDirections)
            {
                var adjacent = tile + offset;

                if (!floorTiles.Contains(adjacent) ||
                    blockedTiles.Contains(adjacent) ||
                    exterior.Contains(adjacent))
                {
                    continue;
                }

                exterior.Add(adjacent);
                frontier.Enqueue(adjacent);
            }
        }

        floorTiles.ExceptWith(exterior);
        floorTiles.RemoveWhere(blockedTiles.Contains);

        foreach (var wall in blockedTiles)
        {
            foreach (var offset in CardinalDirections)
            {
                if (!floorTiles.Contains(wall + offset))
                    continue;

                floorTiles.Add(wall);
                break;
            }
        }

        return new EnclosedTileCache(floorTiles, expires);
    }

    private sealed record EnclosedTileCache(
        HashSet<Vector2i> Tiles,
        TimeSpan Expires);
}

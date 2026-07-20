using System.Numerics;
using Content.Shared.Light.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

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
        var tiles = _mapSystem.GetTilesEnumerator(grid.Owner, grid.Comp, sourceBounds);

        while (tiles.MoveNext(out var tile))
        {
            if (!hasImplicitRoof &&
                (roof == null || _roofSystem.GetColor((grid.Owner, grid.Comp, roof), tile.GridIndices) == null))
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

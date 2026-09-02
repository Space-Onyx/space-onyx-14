using System.Numerics;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Onyx.Clothing.Modsuits;

public sealed partial class MesonVisionOverlay : Overlay
{
    private static readonly Color TerrainFill = Color.FromHex("#103b2c").WithAlpha(0.10f);
    private static readonly Color TerrainOutline = Color.FromHex("#68d99a").WithAlpha(0.24f);
    private static readonly Color StructureColor = Color.FromHex("#75ffad").WithAlpha(0.68f);

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly HashSet<Entity<OccluderComponent>> _structures = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MesonVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _lookup = _entityManager.System<EntityLookupSystem>();
        _map = _entityManager.System<SharedMapSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        args.Viewport.Eye == _eyeManager.CurrentEye &&
        _player.LocalEntity is { Valid: true } player &&
        _entityManager.HasComponent<MesonVisionComponent>(player);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return;
        DrawTerrain(args);
        DrawStructures(args);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTerrain(in OverlayDrawArgs args)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldBounds, ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            args.WorldHandle.SetTransform(_transform.GetWorldMatrix(grid.Owner));
            var tiles = _map.GetTilesEnumerator(grid.Owner, grid.Comp, args.WorldBounds);
            while (tiles.MoveNext(out var tile))
            {
                if (tile.Tile.IsEmpty)
                    continue;
                var bottomLeft = new Vector2(
                    tile.GridIndices.X * grid.Comp.TileSize,
                    tile.GridIndices.Y * grid.Comp.TileSize);
                var bounds = Box2.FromDimensions(bottomLeft, new Vector2(grid.Comp.TileSize));
                args.WorldHandle.DrawRect(bounds, TerrainFill);
                args.WorldHandle.DrawRect(bounds, TerrainOutline, filled: false);
            }
        }
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawStructures(in OverlayDrawArgs args)
    {
        _structures.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldBounds, _structures);
        foreach (var structure in _structures)
        {
            if (!structure.Comp.Enabled)
                continue;
            args.WorldHandle.DrawRect(_lookup.GetWorldAABB(structure.Owner), StructureColor, filled: false);
        }
    }
}

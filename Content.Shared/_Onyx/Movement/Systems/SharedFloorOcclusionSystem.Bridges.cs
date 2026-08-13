using Content.Shared.Movement.Components;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedFloorOcclusionSystem
{
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    private bool IsOcclusionBlocked(Entity<FloorOccluderComponent> entity)
    {
        if (entity.Comp.IgnoreWhenOnTileWithTag is not { } tag)
            return false;

        var xform = Transform(entity.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var tile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var anchoredEntity))
        {
            if (_tagSystem.HasTag(anchoredEntity.Value, tag))
                return true;
        }

        return false;
    }
}

using Content.Shared.Movement.Components;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;

namespace Content.Shared.Movement.Systems;

public sealed partial class SpeedModifierContactsSystem
{
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    private bool IsIgnoredByContact(EntityUid source, SpeedModifierContactsComponent component)
    {
        if (component.IgnoreWhenContactingTag is not { } tag)
            return false;

        var xform = Transform(source);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var tile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var entity))
        {
            if (_tagSystem.HasTag(entity.Value, tag))
                return true;
        }

        return false;
    }
}

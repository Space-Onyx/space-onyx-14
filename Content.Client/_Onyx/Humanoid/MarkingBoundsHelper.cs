// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Humanoid;

/// <summary>
/// Gameplay sprite bounds that ignore humanoid marking layers.
/// The engine unions bounds of every drawn layer, so oversized marking art
/// (tails, horns, ...) inflates mob bounds and breaks overhead bars, status icons,
/// click boxes and sprite previews. This helper mirrors the engine math
/// while skipping layers tracked by <see cref="MarkingLayersComponent"/>.
/// Entities without markings get exactly the engine bounds.
/// </summary>
public static class MarkingBoundsHelper
{
    public static Box2 GetLocalBoundsWithoutMarkings(
        Entity<SpriteComponent?> sprite,
        SpriteSystem spriteSystem,
        IEntityManager entMan)
    {
        var comp = sprite.Comp;
        if (comp == null)
            return new Box2();

        if (!entMan.TryGetComponent<MarkingLayersComponent>(sprite.Owner, out var markings) || markings.LayerIds.Count == 0)
            return spriteSystem.GetLocalBounds(new Entity<SpriteComponent>(sprite.Owner, comp));

        var excluded = new HashSet<int>();
        foreach (var layerId in markings.LayerIds)
        {
            if (spriteSystem.LayerMapTryGet(sprite, layerId, out var markingIndex, false))
                excluded.Add(markingIndex);
        }

        var bounds = new Box2();
        var index = 0;
        foreach (var layer in comp.AllLayers)
        {
            if (layer.Visible && (layer.PixelSize.X > 0 && layer.PixelSize.Y > 0) && !excluded.Contains(index))
                bounds = bounds.Union(layer.CalculateBoundingBox());
            index++;
        }

        return bounds.Scale(Vector2.Abs(comp.Scale));
    }

    public static Box2Rotated CalculateBoundsWithoutMarkings(
        Entity<SpriteComponent?> sprite,
        SpriteSystem spriteSystem,
        IEntityManager entMan,
        Vector2 worldPos,
        Angle worldRot,
        Angle eyeRot)
    {
        var comp = sprite.Comp;
        if (comp == null || !comp.Visible || !comp.AllLayers.Any())
            return new Box2Rotated(new Box2(worldPos, worldPos), Angle.Zero, worldPos);

        worldRot = worldRot.Reduced();
        if (worldRot.Theta < 0)
            worldRot = new Angle(worldRot.Theta + MathF.Tau);

        var finalRotation = comp.NoRotation
            ? comp.Rotation - eyeRot
            : comp.Rotation + worldRot;

        var bounds = GetLocalBoundsWithoutMarkings(sprite, spriteSystem, entMan);

        if (comp.Offset == Vector2.Zero)
            return new Box2Rotated(bounds.Translated(worldPos), finalRotation, worldPos);

        var adjustedOffset = comp.NoRotation
            ? (-eyeRot).RotateVec(comp.Offset)
            : worldRot.RotateVec(comp.Offset);

        var position = adjustedOffset + worldPos;
        return new Box2Rotated(bounds.Translated(position), finalRotation, position);
    }
}

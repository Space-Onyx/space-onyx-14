// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using System.Numerics;
using Content.Shared._Onyx.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
    private static readonly Dictionary<RSI.State, Box2?[]> DirectionalBounds = new();

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

    public static Box2Rotated CalculateDirectionalBounds(
        Entity<SpriteComponent?> sprite,
        IResourceManager resources,
        Vector2 worldPos,
        Angle worldRot,
        Angle eyeRot,
        Direction? overrideDirection)
    {
        var comp = sprite.Comp;
        if (comp == null || !comp.Visible || !comp.AllLayers.Any())
            return new Box2Rotated(new Box2(worldPos, worldPos), Angle.Zero, worldPos);

        var bounds = new Box2();
        foreach (var baseLayer in comp.AllLayers)
        {
            if (baseLayer is not SpriteComponent.Layer { Visible: true, Blank: false } layer ||
                layer.CopyToShaderParameters != null)
                continue;

            var layerBounds = GetDirectionalLayerBounds(layer, resources, worldRot + eyeRot, overrideDirection, out var drawDirection);
            if (layerBounds != null)
            {
                layer.GetLayerDrawMatrix(drawDirection, out var layerMatrix);
                bounds = bounds.Union(layerMatrix.TransformBox(layerBounds.Value));
            }
        }

        bounds = bounds.Scale(Vector2.Abs(comp.Scale));
        var finalRotation = comp.NoRotation
            ? comp.Rotation - eyeRot
            : comp.Rotation + worldRot;

        var adjustedOffset = comp.NoRotation
            ? (-eyeRot).RotateVec(comp.Offset)
            : worldRot.RotateVec(comp.Offset);
        var position = adjustedOffset + worldPos;
        return new Box2Rotated(bounds.Translated(position), finalRotation, position);
    }

    private static Box2? GetDirectionalLayerBounds(
        SpriteComponent.Layer layer,
        IResourceManager resources,
        Angle angle,
        Direction? overrideDirection,
        out RsiDirection drawDirection)
    {
        if (layer.ActualState is not { } state || !layer.State.IsValid)
        {
            drawDirection = RsiDirection.South;
            return GetFrameBounds(layer.PixelSize);
        }

        drawDirection = SpriteComponent.Layer.GetDirection(state.RsiDirections, angle.Reduced().FlipPositive());
        var direction = overrideDirection?.Convert(state.RsiDirections) ?? drawDirection;
        direction = direction.OffsetRsiDir(layer.DirOffset);
        if (!DirectionalBounds.TryGetValue(state, out var stateBounds))
            stateBounds = DirectionalBounds[state] = LoadDirectionalBounds(state, resources);

        return stateBounds[(int) direction];
    }

    private static Box2?[] LoadDirectionalBounds(RSI.State state, IResourceManager resources)
    {
        var directionCount = state.RsiDirections switch
        {
            RsiDirectionType.Dir1 => 1,
            RsiDirectionType.Dir4 => 4,
            RsiDirectionType.Dir8 => 8,
            _ => 1,
        };
        var fallback = GetFrameBounds(state.Size);
        if (!resources.TryContentFileRead(state.RSI.Path / (state.StateId.Name + ".png"), out var stream))
            return Enumerable.Repeat<Box2?>(fallback, directionCount).ToArray();

        using (stream)
        using (var image = Image.Load<Rgba32>(stream))
            return CalculateDirectionalBounds(state, image, directionCount);
    }

    private static Box2?[] CalculateDirectionalBounds(RSI.State state, Image<Rgba32> image, int directionCount)
    {
        var frameSize = state.Size;
        var columns = image.Width / frameSize.X;
        var framesPerDirection = image.Width * image.Height / (frameSize.X * frameSize.Y * directionCount);
        var pixels = image.GetPixelSpan();
        var bounds = new Box2?[directionCount];

        for (var directionIndex = 0; directionIndex < directionCount; directionIndex++)
        {
            for (var animationFrame = 0; animationFrame < framesPerDirection; animationFrame++)
            {
                var frame = directionIndex * framesPerDirection + animationFrame;
                var frameLeft = frame % columns * frameSize.X;
                var frameTop = frame / columns * frameSize.Y;
                var left = frameSize.X;
                var top = frameSize.Y;
                var right = 0;
                var bottom = 0;

                for (var y = 0; y < frameSize.Y; y++)
                {
                    for (var x = 0; x < frameSize.X; x++)
                    {
                        if (pixels[(frameTop + y) * image.Width + frameLeft + x].A == 0)
                            continue;

                        left = Math.Min(left, x);
                        top = Math.Min(top, y);
                        right = Math.Max(right, x + 1);
                        bottom = Math.Max(bottom, y + 1);
                    }
                }

                if (right != 0 && bottom != 0)
                {
                    var halfSize = (Vector2) frameSize / 2f;
                    var frameBounds = new Box2(
                        (new Vector2(left, frameSize.Y - bottom) - halfSize) / EyeManager.PixelsPerMeter,
                        (new Vector2(right, frameSize.Y - top) - halfSize) / EyeManager.PixelsPerMeter);
                    bounds[directionIndex] = bounds[directionIndex]?.Union(frameBounds) ?? frameBounds;
                }
            }
        }

        return bounds;
    }

    private static Box2 GetFrameBounds(Vector2i pixelSize)
    {
        return Box2.CenteredAround(Vector2.Zero, (Vector2) pixelSize / EyeManager.PixelsPerMeter);
    }
}

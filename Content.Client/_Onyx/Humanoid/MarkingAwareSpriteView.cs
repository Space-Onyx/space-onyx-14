// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Onyx.Humanoid;

/// <summary>
/// SpriteView that fits the sprite using gameplay bounds
/// (<see cref="MarkingBoundsHelper"/>), so mobs with oversized marking art
/// don't render tiny with empty padding around them. The full sprite,
/// markings included, is still drawn.
/// </summary>
[Virtual]
public class MarkingAwareSpriteView : SpriteView
{
    private SpriteSystem? _sprites;
    private SharedTransformSystem? _transform;
    private Vector2 _fittedSize;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateFittedSize();
        return _fittedSize;
    }

    private void UpdateFittedSize()
    {
        if (!ResolveTarget(out var uid, out var sprite, out _))
            return;

        _sprites ??= EntMan.System<SpriteSystem>();

        var spriteBox = MarkingBoundsHelper.CalculateBoundsWithoutMarkings(
                (uid, sprite),
                _sprites,
                EntMan,
                Vector2.Zero,
                WorldRotation ?? Angle.Zero,
                EyeRotation)
            .CalcBoundingBox();

        if (!SpriteOffset)
            spriteBox = spriteBox.Translated(-spriteBox.Center);

        var scale = Scale * EyeManager.PixelsPerMeter;
        var bl = spriteBox.BottomLeft * scale;
        var tr = spriteBox.TopRight * scale;

        tr = Vector2.Max(tr, Vector2.Zero);
        bl = Vector2.Min(bl, Vector2.Zero);
        tr = Vector2.Max(tr, -bl);
        bl = Vector2.Min(bl, -tr);
        var box = new Box2(bl, tr);

        if (WorldRotation != null && EyeRotation == Angle.Zero)
        {
            _fittedSize = box.Size;
            return;
        }

        var size = box.Size;
        var longestSide = MathF.Max(size.X, size.Y);
        var longestRotatedSide = Math.Max(longestSide, (size.X + size.Y) / MathF.Sqrt(2));
        _fittedSize = new Vector2(longestRotatedSide, longestRotatedSide);
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        if (!ResolveTarget(out var uid, out var sprite, out var xform))
            return;

        _sprites ??= EntMan.System<SpriteSystem>();
        _transform ??= EntMan.System<SharedTransformSystem>();

        _sprites.ForceUpdate(uid);

        var stretchVec = Stretch switch
        {
            StretchMode.Fit => Vector2.Min(Size / _fittedSize, Vector2.One),
            StretchMode.Fill => Size / _fittedSize,
            _ => Vector2.One,
        };
        var stretch = MathF.Min(stretchVec.X, stretchVec.Y);

        var offset = SpriteOffset
            ? Vector2.Zero
            : -(-EyeRotation).RotateVec(sprite.Offset * Scale) * new Vector2(1, -1) * EyeManager.PixelsPerMeter;

        var position = PixelSize / 2 + offset * stretch * UIScale;
        var scale = Scale * UIScale * stretch;

        var world = renderHandle.DrawingHandleWorld;
        var oldModulate = world.Modulate;
        world.Modulate *= Modulate * ActualModulateSelf;

        renderHandle.DrawEntity(uid, position, scale, WorldRotation, EyeRotation, OverrideDirection, sprite, xform, _transform);
        world.Modulate = oldModulate;
    }

    private bool ResolveTarget(
        out EntityUid uid,
        [NotNullWhen(true)] out SpriteComponent? sprite,
        [NotNullWhen(true)] out TransformComponent? xform)
    {
        if (NetEnt != null && Entity == null && EntMan.TryGetEntity(NetEnt, out var ent))
            SetEntity(ent);

        if (Entity != null)
        {
            (uid, sprite, xform) = Entity.Value;
            return !EntMan.Deleted(uid);
        }

        sprite = null;
        xform = null;
        uid = default;
        return false;
    }
}

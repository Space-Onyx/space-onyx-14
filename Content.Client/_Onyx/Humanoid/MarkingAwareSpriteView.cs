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
using Robust.Shared.ContentPack;

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
    private readonly IResourceManager _resources;
    private SpriteSystem? _sprites;
    private SharedTransformSystem? _transform;
    private Vector2 _fittedSize;
    private Box2 _measuredBox;

    public MarkingAwareSpriteView()
    {
        _resources = IoCManager.Resolve<IResourceManager>();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateSizes();
        return Vector2.Max(_fittedSize, _measuredBox.Size);
    }

    private void UpdateSizes()
    {
        if (!ResolveTarget(out var uid, out var sprite, out _))
            return;

        _sprites ??= EntMan.System<SpriteSystem>();

        var worldRotation = WorldRotation ?? Angle.Zero;
        var fittedBox = MarkingBoundsHelper.CalculateBoundsWithoutMarkings(
                (uid, sprite),
                _sprites,
                EntMan,
                Vector2.Zero,
                worldRotation,
                EyeRotation)
            .CalcBoundingBox();
        var measuredBox = MarkingBoundsHelper.CalculateDirectionalBounds(
                (uid, sprite),
                _resources,
                Vector2.Zero,
                worldRotation,
                EyeRotation,
                OverrideDirection)
            .CalcBoundingBox();

        if (!SpriteOffset)
        {
            fittedBox = fittedBox.Translated(-fittedBox.Center);
        }

        _fittedSize = GetScaledSize(fittedBox);
        _measuredBox = GetScaledBox(measuredBox);
    }

    private Vector2 GetScaledSize(Box2 spriteBox)
    {
        var box = GetScaledBox(spriteBox);
        var bl = box.BottomLeft;
        var tr = box.TopRight;

        tr = Vector2.Max(tr, Vector2.Zero);
        bl = Vector2.Min(bl, Vector2.Zero);
        tr = Vector2.Max(tr, -bl);
        bl = Vector2.Min(bl, -tr);
        box = new Box2(bl, tr);

        if (WorldRotation != null && EyeRotation == Angle.Zero)
            return box.Size;

        var size = box.Size;
        var longestSide = MathF.Max(size.X, size.Y);
        var longestRotatedSide = Math.Max(longestSide, (size.X + size.Y) / MathF.Sqrt(2));
        return new Vector2(longestRotatedSide, longestRotatedSide);
    }

    private Box2 GetScaledBox(Box2 spriteBox)
    {
        var scale = Scale * EyeManager.PixelsPerMeter;
        return new Box2(spriteBox.BottomLeft * scale, spriteBox.TopRight * scale);
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

        var boundsOffset = new Vector2(-_measuredBox.Center.X, _measuredBox.Center.Y);
        var position = PixelSize / 2 + (boundsOffset + offset) * stretch * UIScale;
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

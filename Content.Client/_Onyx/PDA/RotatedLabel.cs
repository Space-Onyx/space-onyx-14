// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.PDA;

/// <summary>
/// Single-line label drawn rotated -90 degrees, reads bottom-to-top.
/// For narrow sidebar tabs where horizontal text would eat precious space.
/// </summary>
public sealed class RotatedLabel : Label
{
    private static readonly Angle Rotation = Angle.FromDegrees(-90);

    public RotatedLabel()
    {
        Align = AlignMode.Center;
        VAlign = VAlignMode.Center;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        return new Vector2(measured.Y, measured.X);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var old = handle.GetTransform();
        var center = Size * UIScale / 2f;
        var offset = Rotation.RotateVec(center) - center;
        handle.SetTransform(GlobalPixelPosition - offset, Rotation);
        base.Draw(handle);
        handle.SetTransform(old);
    }
}

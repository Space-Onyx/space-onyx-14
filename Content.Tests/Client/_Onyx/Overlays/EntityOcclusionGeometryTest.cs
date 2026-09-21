// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Onyx.Overlays;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Onyx.Overlays;

[TestFixture]
[TestOf(typeof(EntityOcclusionOverlay))]
public sealed class EntityOcclusionGeometryTest
{
    private static readonly Vector2[] DiagonalWall =
    [
        new(-0.5f, -0.5f),
        new(0.5f, 0.5f),
        new(0.5f, -0.5f),
    ];

    private static readonly Vector2[] SquareWall =
    [
        new(-0.5f, -0.5f),
        new(0.5f, -0.5f),
        new(0.5f, 0.5f),
        new(-0.5f, 0.5f),
    ];

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void DiagonalWallDoesNotOccludeMissingCorner(int degrees)
    {
        var rotation = Angle.FromDegrees(degrees);
        var matrix = Matrix3x2.CreateRotation((float) rotation.Theta);
        var eye = rotation.RotateVec(new Vector2(-2f, 0f));
        var visiblePoint = rotation.RotateVec(new Vector2(1f, 0.7f));

        Assert.That(
            EntityOcclusionOverlay.IntersectsPolygon(eye, visiblePoint, DiagonalWall, matrix),
            Is.False);
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void DiagonalWallOccludesFilledSide(int degrees)
    {
        var rotation = Angle.FromDegrees(degrees);
        var matrix = Matrix3x2.CreateRotation((float) rotation.Theta);
        var eye = rotation.RotateVec(new Vector2(-2f, 0f));
        var coveredPoint = rotation.RotateVec(new Vector2(1f, 0f));

        Assert.That(
            EntityOcclusionOverlay.IntersectsPolygon(eye, coveredPoint, DiagonalWall, matrix),
            Is.True);
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void SquareWallOccludesSameCornerRay(int degrees)
    {
        var rotation = Angle.FromDegrees(degrees);
        var matrix = Matrix3x2.CreateRotation((float) rotation.Theta);
        var eye = rotation.RotateVec(new Vector2(-2f, 0f));
        var coveredPoint = rotation.RotateVec(new Vector2(1f, 0.7f));

        Assert.That(
            EntityOcclusionOverlay.IntersectsPolygon(eye, coveredPoint, SquareWall, matrix),
            Is.True);
    }
}

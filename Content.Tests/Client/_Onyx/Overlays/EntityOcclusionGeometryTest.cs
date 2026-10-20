// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Onyx.Overlays;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Collision.Shapes;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Tests.Client._Onyx.Overlays;

[TestFixture]
[TestOf(typeof(EntityOcclusionFootprint))]
public sealed class EntityOcclusionGeometryTest
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public void MaskRowsUseBottomOriginTexelCenters(int row)
    {
        var size = new Vector2i(2048, 4);
        var uv = EntityOcclusionOverlay.GetMaskUv(size, row);

        Assert.That(uv.X * size.X, Is.EqualTo(0.5f));
        Assert.That(uv.Y * size.Y, Is.EqualTo(size.Y - row - 0.5f));
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void PolygonKeepsExactVerticesUnderRotation(int degrees)
    {
        var polygon = new PolygonShape();
        polygon.SetAsBox(0.35f, 0.2f);
        var transform = new PhysicsTransform(new Vector2(3f, -2f), Angle.FromDegrees(degrees));
        var footprints = new List<EntityOcclusionFootprint>();
        EntityOcclusionFootprint.Append(polygon, transform, footprints);

        Assert.That(footprints, Has.Count.EqualTo(1));
        Assert.That(footprints[0].Vertices, Has.Length.EqualTo(polygon.Vertices.Length));
        for (var i = 0; i < polygon.Vertices.Length; i++)
            Assert.That(footprints[0].Vertices![i], Is.EqualTo(PhysicsTransform.Mul(transform, polygon.Vertices[i])));
    }

    [Test]
    public void CircleKeepsRadiusAndRotatesLocalOffset()
    {
        var circle = new PhysShapeCircle(0.35f, new Vector2(0.2f, 0.1f));
        var transform = new PhysicsTransform(new Vector2(3f, -2f), Angle.FromDegrees(90));
        var footprints = new List<EntityOcclusionFootprint>();
        EntityOcclusionFootprint.Append(circle, transform, footprints);

        Assert.That(footprints, Has.Count.EqualTo(1));
        Assert.That(footprints[0].Vertices, Is.Null);
        Assert.That(footprints[0].Center, Is.EqualTo(PhysicsTransform.Mul(transform, circle.Position)));
        Assert.That(footprints[0].Radius, Is.EqualTo(circle.Radius));
    }

    [TestCase(0.8f, 0.6f)]
    [TestCase(0.8f, -0.6f)]
    [TestCase(0.8f, 0f)]
    [TestCase(0f, 0.6f)]
    [TestCase(0f, 0f)]
    public void EdgeKeepsEndpointsWithoutUsingBoundingBoxCorners(float dx, float dy)
    {
        var start = new Vector2(-0.2f, 0.1f);
        var end = start + new Vector2(dx, dy);
        var shape = new EdgeShape(start, end);
        var transform = new PhysicsTransform(Vector2.One, Angle.FromDegrees(45));
        var footprints = new List<EntityOcclusionFootprint>();
        EntityOcclusionFootprint.Append(shape, transform, footprints);

        Assert.That(footprints, Has.Count.EqualTo(1));
        var actual = footprints[0].Vertices!;
        var expectedStart = PhysicsTransform.Mul(transform, start);
        var expectedEnd = PhysicsTransform.Mul(transform, end);
        var forward = Vector2.Distance(actual[0], expectedStart) + Vector2.Distance(actual[1], expectedEnd);
        var reverse = Vector2.Distance(actual[1], expectedStart) + Vector2.Distance(actual[0], expectedEnd);
        Assert.That(MathF.Min(forward, reverse), Is.LessThan(0.00001f));
    }

    [Test]
    public void AabbRemainsOrientedRatherThanExpandingToWorldBounds()
    {
        var shape = new PhysShapeAabb();
        var transform = new PhysicsTransform(Vector2.One, Angle.FromDegrees(45));
        var footprints = new List<EntityOcclusionFootprint>();
        EntityOcclusionFootprint.Append(shape, transform, footprints);

        Assert.That(footprints[0].Vertices, Has.Length.EqualTo(4));
        Assert.That(footprints[0].Vertices![0], Is.EqualTo(PhysicsTransform.Mul(transform, shape.LocalBounds.BottomLeft)));
        Assert.That(Vector2.Distance(footprints[0].Vertices![0], footprints[0].Vertices![1]),
            Is.EqualTo(shape.LocalBounds.Width).Within(0.00001f));
    }
}

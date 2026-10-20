// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Physics.Collision.Shapes;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Client._Onyx.Overlays;

internal readonly record struct EntityOcclusionFootprint(Vector2 Center, float Radius, Vector2[]? Vertices = null)
{
    internal static void Append(IPhysShape shape, PhysicsTransform transform, List<EntityOcclusionFootprint> output)
    {
        switch (shape)
        {
            case PhysShapeCircle circle:
                output.Add(new EntityOcclusionFootprint(PhysicsTransform.Mul(transform, circle.Position), circle.Radius));
                break;
            case PolygonShape polygon:
                AppendPolygon(polygon.Vertices, transform, output);
                break;
            case PhysShapeAabb aabb:
                var bounds = aabb.LocalBounds;
                AppendPolygon([bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft], transform, output);
                break;
            case EdgeShape edge:
                // Edge vertices are internal. Two public AABB projections recover the exact unordered endpoints.
                var local = edge.ComputeAABB(PhysicsTransform.Empty, 0).Enlarged(-edge.Radius);
                var rotated = edge.ComputeAABB(new PhysicsTransform(MathF.PI / 4f), 0);
                if (rotated.Width <= rotated.Height)
                    AppendPolygon([local.BottomLeft, local.TopRight], transform, output);
                else
                    AppendPolygon([local.TopLeft, local.BottomRight], transform, output);
                break;
            case ChainShape chain:
                for (var i = 0; i < chain.ChildCount; i++)
                {
                    AppendPolygon([chain.Vertices[i], chain.Vertices[i + 1]], transform, output);
                }
                break;
        }
    }

    private static void AppendPolygon(Vector2[] vertices, PhysicsTransform transform,
        List<EntityOcclusionFootprint> output)
    {
        var world = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            world[i] = PhysicsTransform.Mul(transform, vertices[i]);
        output.Add(new EntityOcclusionFootprint(default, 0f, world));
    }
}

using System.Numerics;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;
using PhysTransform = Robust.Shared.Physics.Transform;

namespace Content.Client._Onyx.Surgery.Augments;

internal static class CyberDeckScriptOverlayHelper
{
    public readonly record struct HighlightShape(Vector2[] Vertices, Vector2[] OuterVertices, Box2 Bounds);

    public static bool TryGetActiveScript<TScript>(
        IEntityManager entityManager,
        IPlayerManager player,
        IUserInterfaceManager ui,
        out EntityUid body,
        out float range,
        out TScript script)
        where TScript : Component
    {
        body = default;
        range = 0f;
        script = default!;
        if (player.LocalEntity is not { Valid: true } user)
            return false;

        var actionUi = ui.GetUIController<ActionUIController>();
        if (actionUi.SelectingTargetFor is not { } action ||
            !entityManager.TryGetComponent(action, out ActionComponent? actionComp) ||
            actionComp.AttachedEntity != user || actionComp.Container is not { } container ||
            !entityManager.TryGetComponent(container, out TScript? resolvedScript) || resolvedScript == null ||
            !entityManager.TryGetComponent(action, out TargetActionComponent? targetAction))
            return false;

        body = user;
        script = resolvedScript;
        range = MathF.Max(0f, targetAction.Range);
        return range > 0f;
    }

    public static bool TryBuildShape(
        IEntityManager entityManager,
        SharedTransformSystem transform,
        EntityUid target,
        out HighlightShape result)
    {
        result = default;
        if (!entityManager.TryGetComponent(target, out FixturesComponent? fixtures) || fixtures.Fixtures.Count == 0 ||
            !entityManager.TryGetComponent(target, out TransformComponent? xform))
            return false;

        Fixture? fixture = null;
        if (!fixtures.Fixtures.TryGetValue("fix1", out fixture))
        {
            foreach (var candidate in fixtures.Fixtures.Values)
            {
                fixture = candidate;
                if (candidate.Hard)
                    break;
            }
        }

        if (fixture == null)
            return false;

        var world = new PhysTransform(transform.GetWorldPosition(xform), transform.GetWorldRotation(xform));
        var vertices = GetVertices(fixture.Shape, world);
        if (vertices.Length < 3)
            return false;

        var center = Vector2.Zero;
        foreach (var vertex in vertices)
            center += vertex;
        center /= vertices.Length;

        var outer = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var direction = vertices[i] - center;
            outer[i] = direction.LengthSquared() <= 0.0001f
                ? vertices[i]
                : vertices[i] + Vector2.Normalize(direction) * 0.03f;
        }

        var min = vertices[0];
        var max = vertices[0];
        for (var i = 1; i < vertices.Length; i++)
        {
            min = Vector2.Min(min, vertices[i]);
            max = Vector2.Max(max, vertices[i]);
        }

        result = new HighlightShape(vertices, outer, new Box2(min, max));
        return true;
    }

    public static void Draw(DrawingHandleWorld handle, in HighlightShape shape, Color fill, Color outer, Color inner)
    {
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, shape.Vertices, fill);
        DrawOutline(handle, shape.OuterVertices, outer);
        DrawOutline(handle, shape.Vertices, inner);
    }

    private static Vector2[] GetVertices(IPhysShape shape, PhysTransform transform)
    {
        switch (shape)
        {
            case PolygonShape polygon:
                var polygonVertices = new Vector2[polygon.VertexCount];
                for (var i = 0; i < polygon.VertexCount; i++)
                    polygonVertices[i] = PhysTransform.Mul(transform, polygon.Vertices[i]);
                return polygonVertices;
            case PhysShapeCircle circle:
                const int segments = 20;
                var circleVertices = new Vector2[segments];
                for (var i = 0; i < segments; i++)
                {
                    var angle = i / (float) segments * MathF.PI * 2f;
                    circleVertices[i] = PhysTransform.Mul(transform,
                        circle.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circle.Radius);
                }
                return circleVertices;
            case PhysShapeAabb aabb:
                return
                [
                    PhysTransform.Mul(transform, aabb.LocalBounds.BottomLeft),
                    PhysTransform.Mul(transform, aabb.LocalBounds.BottomRight),
                    PhysTransform.Mul(transform, aabb.LocalBounds.TopRight),
                    PhysTransform.Mul(transform, aabb.LocalBounds.TopLeft),
                ];
            default:
                var bounds = shape.ComputeAABB(transform, 0);
                return [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft];
        }
    }

    private static void DrawOutline(DrawingHandleWorld handle, IReadOnlyList<Vector2> vertices, Color color)
    {
        for (var i = 0; i < vertices.Count; i++)
            handle.DrawLine(vertices[i], vertices[(i + 1) % vertices.Count], color);
    }
}

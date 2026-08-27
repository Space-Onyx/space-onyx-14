using System.Linq;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Research.UI;

public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    private const float NodeSize = 80f;
    private const float LongConnectionDistance = 8f;
    private const float RoutePadding = 18f;
    private const float StubGridLength = 1.25f;
    private const float StubSlotStep = 0.4f;

    private static readonly Color ConnectionColor = Color.FromHex("#A8B4C4").WithAlpha(0.7f);

    private readonly record struct NodeRect(float Left, float Top, float Right, float Bottom)
    {
        public Vector2 Center => new((Left + Right) / 2f, (Top + Bottom) / 2f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var items = Children.OfType<FancyResearchConsoleItem>().ToArray();
        if (items.Length == 0)
            return;

        var byId = items.ToDictionary(item => item.Prototype.ID);
        var bounds = items.ToDictionary(item => item, GetTreeBounds);
        var stubSlots = new Dictionary<string, int>();
        var scale = items[0].PixelWidth / NodeSize;
        var origin = items[0].PixelPosition - items[0].TreePosition * scale;

        foreach (var item in items)
        {
            foreach (var prerequisiteId in item.Prototype.TechnologyPrerequisites)
            {
                if (!byId.TryGetValue(prerequisiteId, out var prerequisite))
                    continue;

                var delta = item.Prototype.Position - prerequisite.Prototype.Position;
                if (Math.Abs(delta.X) + Math.Abs(delta.Y) > LongConnectionDistance)
                {
                    DrawLongConnection(handle, prerequisite, item, bounds[prerequisite], bounds[item], bounds.Values,
                        stubSlots, origin, scale);
                    continue;
                }

                DrawRoute(handle, TransformRoute(SelectRoute(bounds[prerequisite], bounds[item], bounds.Values), origin, scale));
            }
        }
    }

    private static IReadOnlyList<Vector2> SelectRoute(NodeRect source, NodeRect target, IEnumerable<NodeRect> obstacles)
    {
        var routes = new List<IReadOnlyList<Vector2>>
        {
            CreateHorizontalRoute(source, target, (source.Center.X + target.Center.X) / 2f),
            CreateVerticalRoute(source, target, (source.Center.Y + target.Center.Y) / 2f),
        };

        var obstacleArray = obstacles.ToArray();
        foreach (var obstacle in obstacleArray)
        {
            if (obstacle == source || obstacle == target)
                continue;

            routes.Add(CreateHorizontalRoute(source, target, obstacle.Left - RoutePadding));
            routes.Add(CreateHorizontalRoute(source, target, obstacle.Right + RoutePadding));
            routes.Add(CreateVerticalRoute(source, target, obstacle.Top - RoutePadding));
            routes.Add(CreateVerticalRoute(source, target, obstacle.Bottom + RoutePadding));
        }

        return routes
            .Where(route => !IntersectsObstacles(route, source, target, obstacleArray))
            .MinBy(RouteLength) ?? routes.MinBy(RouteLength)!;
    }

    private static IReadOnlyList<Vector2> CreateHorizontalRoute(NodeRect source, NodeRect target, float corridorX)
    {
        var sourceOnLeft = source.Center.X <= target.Center.X;
        var start = new Vector2(sourceOnLeft ? source.Right : source.Left, source.Center.Y);
        var end = new Vector2(sourceOnLeft ? target.Left : target.Right, target.Center.Y);
        return [start, new Vector2(corridorX, start.Y), new Vector2(corridorX, end.Y), end];
    }

    private static IReadOnlyList<Vector2> CreateVerticalRoute(NodeRect source, NodeRect target, float corridorY)
    {
        var sourceAbove = source.Center.Y <= target.Center.Y;
        var start = new Vector2(source.Center.X, sourceAbove ? source.Bottom : source.Top);
        var end = new Vector2(target.Center.X, sourceAbove ? target.Top : target.Bottom);
        return [start, new Vector2(start.X, corridorY), new Vector2(end.X, corridorY), end];
    }

    private static bool IntersectsObstacles(IReadOnlyList<Vector2> route, NodeRect source, NodeRect target, IEnumerable<NodeRect> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            if (obstacle == source || obstacle == target)
                continue;

            var inflated = new NodeRect(obstacle.Left - 4f, obstacle.Top - 4f, obstacle.Right + 4f, obstacle.Bottom + 4f);
            for (var i = 1; i < route.Count; i++)
            {
                if (SegmentIntersects(route[i - 1], route[i], inflated))
                    return true;
            }
        }

        return false;
    }

    private static bool SegmentIntersects(Vector2 start, Vector2 end, NodeRect rect)
    {
        if (MathHelper.CloseTo(start.X, end.X))
            return start.X >= rect.Left && start.X <= rect.Right && Math.Max(start.Y, end.Y) >= rect.Top && Math.Min(start.Y, end.Y) <= rect.Bottom;

        return start.Y >= rect.Top && start.Y <= rect.Bottom && Math.Max(start.X, end.X) >= rect.Left && Math.Min(start.X, end.X) <= rect.Right;
    }

    private static void DrawLongConnection(DrawingHandleScreen handle, FancyResearchConsoleItem sourceItem,
        FancyResearchConsoleItem targetItem, NodeRect source, NodeRect target, IEnumerable<NodeRect> obstacles,
        Dictionary<string, int> stubSlots, Vector2 origin, float viewScale)
    {
        var delta = targetItem.Prototype.Position - sourceItem.Prototype.Position;
        var horizontal = Math.Abs(delta.X) >= Math.Abs(delta.Y);
        var direction = horizontal ? Math.Sign(delta.X) : Math.Sign(delta.Y);
        if (direction == 0)
            direction = 1;

        var preferred = (horizontal ? Vector2.UnitX : Vector2.UnitY) * direction;
        var sourceStub = CreateFreeStub(sourceItem.Prototype.ID, source, target, preferred, 1f, obstacles, stubSlots);
        var targetStub = CreateFreeStub(targetItem.Prototype.ID, target, source, -preferred, 1f, obstacles, stubSlots);

        DrawRoute(handle, TransformRoute(sourceStub, origin, viewScale));
        DrawRoute(handle, TransformRoute(targetStub, origin, viewScale));
        DrawPortalIcon(handle, origin + sourceStub[^1] * viewScale, viewScale, targetItem.ResearchTexture);
        DrawPortalIcon(handle, origin + targetStub[^1] * viewScale, viewScale, sourceItem.ResearchTexture);
    }

    private static IReadOnlyList<Vector2> CreateFreeStub(string id, NodeRect source, NodeRect target,
        Vector2 preferred, float scale, IEnumerable<NodeRect> obstacles, Dictionary<string, int> stubSlots)
    {
        var directions = new[] { preferred, new Vector2(-preferred.Y, preferred.X), new Vector2(preferred.Y, -preferred.X), -preferred };
        foreach (var direction in directions)
        {
            var start = GetEdgePoint(source, direction);
            var key = $"{id}:{direction.X},{direction.Y}";
            var slot = stubSlots.GetValueOrDefault(key);
            var turn = start + direction * StubGridLength * 150f * scale;
            var side = slot % 2 == 0 ? 1f : -1f;
            var row = slot / 2 + 1;
            var lateral = new Vector2(-direction.Y, direction.X) * side * row * StubSlotStep * 150f * scale;
            var end = turn + lateral;
            if (!IntersectsObstacles([start, turn, end], source, target, obstacles))
            {
                stubSlots[key] = slot + 1;
                return [start, turn, end];
            }
        }

        var fallbackStart = GetEdgePoint(source, preferred);
        var fallbackKey = $"{id}:{preferred.X},{preferred.Y}";
        var fallbackSlot = stubSlots.GetValueOrDefault(fallbackKey);
        stubSlots[fallbackKey] = fallbackSlot + 1;
        var fallbackTurn = fallbackStart + preferred * StubGridLength * 150f * scale;
        var fallbackSide = fallbackSlot % 2 == 0 ? 1f : -1f;
        var fallbackRow = fallbackSlot / 2 + 1;
        var fallbackLateral = new Vector2(-preferred.Y, preferred.X) * fallbackSide * fallbackRow * StubSlotStep * 150f * scale;
        return [fallbackStart, fallbackTurn, fallbackTurn + fallbackLateral];
    }

    private static Vector2 GetEdgePoint(NodeRect source, Vector2 direction)
    {
        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
            return new Vector2(direction.X > 0 ? source.Right : source.Left, source.Center.Y);
        return new Vector2(source.Center.X, direction.Y > 0 ? source.Bottom : source.Top);
    }

    private static void DrawPortalIcon(DrawingHandleScreen handle, Vector2 center, float scale, Texture? texture)
    {
        var size = new Vector2(28f) * scale;
        var bounds = UIBox2.FromDimensions(center - size / 2f, size);
        handle.DrawRect(bounds, Color.FromHex("#101216"));
        handle.DrawRect(bounds, ConnectionColor, false);
        if (texture != null)
            handle.DrawTextureRect(texture, new UIBox2(bounds.Left + 3f * scale, bounds.Top + 3f * scale,
                bounds.Right - 3f * scale, bounds.Bottom - 3f * scale));
    }

    private static NodeRect GetTreeBounds(FancyResearchConsoleItem item)
    {
        return new NodeRect(item.TreePosition.X, item.TreePosition.Y,
            item.TreePosition.X + NodeSize, item.TreePosition.Y + NodeSize);
    }

    private static IReadOnlyList<Vector2> TransformRoute(IReadOnlyList<Vector2> route, Vector2 origin, float scale)
        => route.Select(point => origin + point * scale).ToArray();

    private static void DrawRoute(DrawingHandleScreen handle, IReadOnlyList<Vector2> route)
    {
        for (var i = 1; i < route.Count; i++)
            handle.DrawLine(route[i - 1], route[i], ConnectionColor);
    }

    private static float RouteLength(IReadOnlyList<Vector2> route)
    {
        var length = 0f;
        for (var i = 1; i < route.Count; i++)
            length += Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y);
        return length;
    }
}

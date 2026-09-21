using System.Linq;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Onyx.Research.UI;

public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    public event Action<TechnologyPrototype>? TechnologyTerminalPressed;

    private const float NodeSize = 80f;
    private const float LongConnectionDistance = 8f;
    private const float RoutePadding = 18f;
    private const float StubGridLength = 0.4f;
    private const float StubSlotStep = 0.4f;
    private const float TerminalClearance = 36f;
    private const float TerminalSize = 24f;
    private const float TerminalPortSpacing = 14f;
    private const float BendPenalty = 42f;
    private const float CrossingPenalty = 180f;
    private const float OverlapPenalty = 7f;
    private const float ObstaclePenalty = 100000f;
    private const float BridgeHalfGap = 5f;
    private const float BridgeCapHalfLength = 4f;
    private const float RouteGridStep = 75f;

    private static readonly Color ConnectionColor = Color.FromHex("#718294").WithAlpha(0.48f);
    private static readonly Color HighlightColor = Color.FromHex("#D9E6F2").WithAlpha(0.95f);
    private static readonly Color TerminalBackground = Color.FromHex("#0B1118").WithAlpha(0.96f);

    private readonly List<RoutedConnection> _connections = new();
    private readonly List<FancyResearchConsoleItem> _routeItems = new();
    private bool _routesInvalid = true;
    public string? FocusedTechnology { get; set; }

    private readonly record struct NodeRect(float Left, float Top, float Right, float Bottom)
    {
        public Vector2 Center => new((Left + Right) / 2f, (Top + Bottom) / 2f);
    }

    private sealed record RoutedConnection(
        FancyResearchConsoleItem Source,
        FancyResearchConsoleItem Target,
        IReadOnlyList<Vector2> SourceRoute,
        IReadOnlyList<Vector2>? TargetRoute,
        int Priority,
        List<RouteBridge> SourceBridges,
        List<RouteBridge> TargetBridges);

    private readonly record struct RouteSegment(Vector2 Start, Vector2 End);
    private readonly record struct OccupiedSegment(
        RouteSegment Segment,
        string Source,
        string Target,
        bool SourceBranch,
        bool TargetBranch);
    private readonly record struct RouteBridge(int Segment, Vector2 Position);
    private readonly record struct ConnectionRequest(
        FancyResearchConsoleItem Source,
        FancyResearchConsoleItem Target,
        int Distance,
        int Priority);
    private readonly record struct GridPoint(int X, int Y);
    private readonly record struct GridState(GridPoint Point, GridPoint Direction);
    private static readonly GridPoint[] GridDirections =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_routesInvalid)
        {
            _routeItems.Clear();
            foreach (var child in Children)
            {
                if (child is FancyResearchConsoleItem item)
                    _routeItems.Add(item);
            }

            if (_routeItems.Count > 0)
            {
                var byId = _routeItems.ToDictionary(item => item.Prototype.ID);
                var bounds = _routeItems.ToDictionary(item => item, GetTreeBounds);
                BuildRoutes(_routeItems, byId, bounds);
            }
            else
                _connections.Clear();

            _routesInvalid = false;
        }

        if (_routeItems.Count == 0)
            return;

        var scale = _routeItems[0].PixelWidth / NodeSize;
        var origin = _routeItems[0].PixelPosition - _routeItems[0].TreePosition * scale;

        foreach (var connection in _connections)
        {
            if (IsFocused(connection))
                continue;
            DrawConnection(handle, connection, origin, scale, ConnectionColor);
        }

        foreach (var connection in _connections)
        {
            if (IsFocused(connection))
                DrawConnection(handle, connection, origin, scale, HighlightColor);
        }
    }

    public void InvalidateRoutes() => _routesInvalid = true;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _routeItems.Count == 0)
            return;

        var scale = _routeItems[0].PixelWidth / NodeSize;
        var origin = _routeItems[0].PixelPosition - _routeItems[0].TreePosition * scale;
        var halfSize = TerminalSize * scale / 2f;
        foreach (var connection in _connections)
        {
            if (connection.TargetRoute == null)
                continue;

            var sourceTerminal = origin + connection.SourceRoute[^1] * scale;
            if (ContainsTerminal(sourceTerminal, halfSize, args.RelativePixelPosition))
            {
                TechnologyTerminalPressed?.Invoke(connection.Target.Prototype);
                args.Handle();
                return;
            }

            var targetTerminal = origin + connection.TargetRoute[^1] * scale;
            if (!ContainsTerminal(targetTerminal, halfSize, args.RelativePixelPosition))
                continue;

            TechnologyTerminalPressed?.Invoke(connection.Source.Prototype);
            args.Handle();
            return;
        }
    }

    private static bool ContainsTerminal(Vector2 center, float halfSize, Vector2 point)
        => point.X >= center.X - halfSize && point.X <= center.X + halfSize &&
           point.Y >= center.Y - halfSize && point.Y <= center.Y + halfSize;

    private bool IsFocused(RoutedConnection connection)
        => connection.Source.IsHovered ||
           connection.Target.IsHovered ||
           connection.Source.Prototype.ID == FocusedTechnology ||
           connection.Target.Prototype.ID == FocusedTechnology;

    private void BuildRoutes(
        IReadOnlyList<FancyResearchConsoleItem> items,
        IReadOnlyDictionary<string, FancyResearchConsoleItem> byId,
        IReadOnlyDictionary<FancyResearchConsoleItem, NodeRect> bounds)
    {
        _connections.Clear();
        var occupied = new List<OccupiedSegment>();
        var terminals = new List<Vector2>();
        var stubSlots = new Dictionary<string, int>();
        var obstacles = bounds.Values.ToArray();
        var dependentCounts = items.ToDictionary(item => item.Prototype.ID, _ => 0);
        foreach (var item in items)
        {
            foreach (var prerequisite in item.Prototype.TechnologyPrerequisites)
            {
                if (dependentCounts.ContainsKey(prerequisite))
                    dependentCounts[prerequisite]++;
            }
        }

        var requests = new List<ConnectionRequest>();
        foreach (var target in items)
        {
            foreach (var prerequisiteId in target.Prototype.TechnologyPrerequisites)
            {
                if (!byId.TryGetValue(prerequisiteId, out var source))
                    continue;
                var delta = target.Prototype.Position - source.Prototype.Position;
                var distance = (int) (Math.Abs(delta.X) + Math.Abs(delta.Y));
                var priority = dependentCounts[source.Prototype.ID] * 100 - distance;
                requests.Add(new ConnectionRequest(source, target, distance, priority));
            }
        }

        foreach (var request in requests.Where(request => request.Distance > LongConnectionDistance)
                     .OrderByDescending(request => request.Priority)
                     .ThenBy(request => request.Source.Prototype.ID)
                     .ThenBy(request => request.Target.Prototype.ID))
        {
            var routes = CreateLongRoutes(request.Source, request.Target, bounds[request.Source], bounds[request.Target],
                obstacles, stubSlots, terminals, occupied);
            if (routes.Source.Count < 2 || routes.Target.Count < 2)
                continue;

            _connections.Add(new RoutedConnection(request.Source, request.Target, routes.Source, routes.Target,
                request.Priority - 1000, new(), new()));
            AddSegments(routes.Source, request.Source.Prototype.ID, request.Target.Prototype.ID, occupied);
            AddSegments(routes.Target, request.Source.Prototype.ID, request.Target.Prototype.ID, occupied);
        }

        foreach (var request in requests.Where(request => request.Distance <= LongConnectionDistance)
                     .OrderByDescending(request => request.Priority)
                     .ThenBy(request => request.Source.Prototype.ID)
                     .ThenBy(request => request.Target.Prototype.ID))
        {
            var route = SelectRoute(bounds[request.Source], bounds[request.Target], obstacles, terminals, occupied,
                request.Source.Prototype.ID, request.Target.Prototype.ID);
            _connections.Add(new RoutedConnection(request.Source, request.Target, route, null, request.Priority, new(), new()));
            AddSegments(route, request.Source.Prototype.ID, request.Target.Prototype.ID, occupied, true);
        }

        BuildBridges();
    }

    private void BuildBridges()
    {
        for (var firstIndex = 0; firstIndex < _connections.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < _connections.Count; secondIndex++)
            {
                var first = _connections[firstIndex];
                var second = _connections[secondIndex];
                var firstIsJumper = first.Priority < second.Priority ||
                                    first.Priority == second.Priority &&
                                    string.CompareOrdinal(first.Target.Prototype.ID, second.Target.Prototype.ID) > 0;
                var jumper = firstIsJumper ? first : second;
                var main = ReferenceEquals(jumper, first) ? second : first;
                AddCrossingBridges(jumper.SourceRoute, jumper.SourceBridges, main.SourceRoute);
                if (main.TargetRoute != null)
                    AddCrossingBridges(jumper.SourceRoute, jumper.SourceBridges, main.TargetRoute);
                if (jumper.TargetRoute == null)
                    continue;
                AddCrossingBridges(jumper.TargetRoute, jumper.TargetBridges, main.SourceRoute);
                if (main.TargetRoute != null)
                    AddCrossingBridges(jumper.TargetRoute, jumper.TargetBridges, main.TargetRoute);
            }
        }

        foreach (var connection in _connections)
        {
            SortBridges(connection.SourceRoute, connection.SourceBridges);
            if (connection.TargetRoute != null)
                SortBridges(connection.TargetRoute, connection.TargetBridges);
        }
    }

    private static void SortBridges(IReadOnlyList<Vector2> route, List<RouteBridge> bridges)
    {
        bridges.Sort((first, second) =>
        {
            var segment = first.Segment.CompareTo(second.Segment);
            return segment != 0
                ? segment
                : Vector2.DistanceSquared(route[first.Segment - 1], first.Position)
                    .CompareTo(Vector2.DistanceSquared(route[second.Segment - 1], second.Position));
        });
    }

    private static void AddCrossingBridges(
        IReadOnlyList<Vector2> jumper,
        ICollection<RouteBridge> bridges,
        IReadOnlyList<Vector2> main)
    {
        for (var jumperSegment = 1; jumperSegment < jumper.Count; jumperSegment++)
        {
            var jumping = new RouteSegment(jumper[jumperSegment - 1], jumper[jumperSegment]);
            for (var mainSegment = 1; mainSegment < main.Count; mainSegment++)
            {
                var fixedSegment = new RouteSegment(main[mainSegment - 1], main[mainSegment]);
                if (!TryGetCrossing(jumping, fixedSegment, out var crossing) ||
                    Vector2.Distance(crossing, jumping.Start) <= BridgeHalfGap ||
                    Vector2.Distance(crossing, jumping.End) <= BridgeHalfGap)
                    continue;
                if (bridges.All(bridge => bridge.Segment != jumperSegment || Vector2.DistanceSquared(bridge.Position, crossing) > 16f * 16f))
                    bridges.Add(new RouteBridge(jumperSegment, crossing));
            }
        }
    }

    private static IReadOnlyList<Vector2> SelectRoute(
        NodeRect source,
        NodeRect target,
        IReadOnlyList<NodeRect> obstacles,
        IReadOnlyList<Vector2> terminals,
        IReadOnlyList<OccupiedSegment> occupied,
        string sourceId,
        string targetId)
    {
        var routes = new List<IReadOnlyList<Vector2>>
        {
            CreateHorizontalRoute(source, target, (source.Center.X + target.Center.X) / 2f),
            CreateVerticalRoute(source, target, (source.Center.Y + target.Center.Y) / 2f),
        };

        foreach (var occupiedSegment in occupied)
        {
            if (occupiedSegment.Source != sourceId || !occupiedSegment.SourceBranch)
                continue;

            var segment = occupiedSegment.Segment;
            if (MathHelper.CloseTo(segment.Start.X, segment.End.X))
                routes.Add(CreateVerticalRoute(source, target, segment.End.Y));
            else
                routes.Add(CreateHorizontalRoute(source, target, segment.End.X));
        }

        foreach (var occupiedSegment in occupied)
        {
            if (occupiedSegment.Target != targetId || !occupiedSegment.TargetBranch)
                continue;

            var segment = occupiedSegment.Segment;
            if (MathHelper.CloseTo(segment.Start.X, segment.End.X))
                routes.Add(CreateVerticalRoute(source, target, segment.Start.Y));
            else
                routes.Add(CreateHorizontalRoute(source, target, segment.Start.X));
        }

        var clearRoutes = routes
            .Where(route => !IntersectsObstacles(route, source, target, obstacles) &&
                            !IntersectsTerminals(route, terminals))
            .ToList();
        if (clearRoutes.Count > 0)
            return clearRoutes.MinBy(route => RouteScore(route, source, target, obstacles, occupied, sourceId, targetId))!;

        if (FindGridRoute(source, target, obstacles, terminals, occupied) is { } gridRoute)
            return gridRoute;

        var outerRoute = CreateOuterRoute(source, target, obstacles);
        return !IntersectsObstacles(outerRoute, source, target, obstacles) && !IntersectsTerminals(outerRoute, terminals)
            ? outerRoute
            : [source.Center];
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

    private static IReadOnlyList<Vector2> CreateOuterRoute(
        NodeRect source,
        NodeRect target,
        IReadOnlyList<NodeRect> obstacles)
    {
        var left = obstacles.Min(obstacle => obstacle.Left) - RoutePadding * 2f;
        var right = obstacles.Max(obstacle => obstacle.Right) + RoutePadding * 2f;
        var top = obstacles.Min(obstacle => obstacle.Top) - RoutePadding * 2f;
        var bottom = obstacles.Max(obstacle => obstacle.Bottom) + RoutePadding * 2f;
        var routes = new[]
        {
            CreateHorizontalRoute(source, target, left),
            CreateHorizontalRoute(source, target, right),
            CreateVerticalRoute(source, target, top),
            CreateVerticalRoute(source, target, bottom),
        };
        return routes.MinBy(RouteLength)!;
    }

    private static IReadOnlyList<Vector2>? FindGridRoute(
        NodeRect source,
        NodeRect target,
        IReadOnlyList<NodeRect> obstacles,
        IReadOnlyList<Vector2> terminals,
        IReadOnlyList<OccupiedSegment> occupied)
    {
        var gridOffset = NodeSize / 2f;
        var minX = (int) MathF.Floor((obstacles.Min(obstacle => obstacle.Left) - RouteGridStep - gridOffset) / RouteGridStep);
        var maxX = (int) MathF.Ceiling((obstacles.Max(obstacle => obstacle.Right) + RouteGridStep - gridOffset) / RouteGridStep);
        var minY = (int) MathF.Floor((obstacles.Min(obstacle => obstacle.Top) - RouteGridStep - gridOffset) / RouteGridStep);
        var maxY = (int) MathF.Ceiling((obstacles.Max(obstacle => obstacle.Bottom) + RouteGridStep - gridOffset) / RouteGridStep);
        var starts = GetGridPorts(source);
        var targets = GetGridPorts(target).ToHashSet();
        var frontier = new List<(GridState State, float Priority)>();
        var costs = new Dictionary<GridState, float>();
        var previous = new Dictionary<GridState, GridState>();
        var visited = new HashSet<GridState>();

        foreach (var start in starts)
        {
            var state = new GridState(start, default);
            costs[state] = 0f;
            Enqueue(frontier, state, 0f);
        }

        while (frontier.Count > 0)
        {
            var current = Dequeue(frontier);
            if (!visited.Add(current))
                continue;
            if (targets.Contains(current.Point))
                return ReconstructGridRoute(current, previous, source, target);

            foreach (var direction in GridDirections)
            {
                var nextPoint = new GridPoint(current.Point.X + direction.X, current.Point.Y + direction.Y);
                if (nextPoint.X < minX || nextPoint.X > maxX || nextPoint.Y < minY || nextPoint.Y > maxY)
                    continue;
                var start = ToRoutePoint(current.Point);
                var end = ToRoutePoint(nextPoint);
                if (SegmentBlocked(start, end, source, target, obstacles, terminals))
                    continue;

                var next = new GridState(nextPoint, direction);
                var bend = current.Direction == default || current.Direction == direction ? 0f : BendPenalty;
                var segment = new RouteSegment(start, end);
                var conflict = occupied.Any(other => SegmentsOverlap(segment, other.Segment))
                    ? OverlapPenalty * RouteGridStep
                    : occupied.Any(other => SegmentsCross(segment, other.Segment)) ? CrossingPenalty : 0f;
                var cost = costs[current] + RouteGridStep + bend + conflict;
                if (costs.TryGetValue(next, out var known) && known <= cost)
                    continue;
                costs[next] = cost;
                previous[next] = current;
                Enqueue(frontier, next, cost + GridDistance(nextPoint, targets) * RouteGridStep * 0.15f);
            }
        }
        return null;
    }

    private static void Enqueue(List<(GridState State, float Priority)> heap, GridState state, float priority)
    {
        heap.Add((state, priority));
        var index = heap.Count - 1;
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (heap[parent].Priority <= priority)
                break;

            heap[index] = heap[parent];
            index = parent;
        }
        heap[index] = (state, priority);
    }

    private static GridState Dequeue(List<(GridState State, float Priority)> heap)
    {
        var result = heap[0].State;
        var last = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0)
            return result;

        var index = 0;
        while (true)
        {
            var left = index * 2 + 1;
            if (left >= heap.Count)
                break;

            var right = left + 1;
            var child = right < heap.Count && heap[right].Priority < heap[left].Priority ? right : left;
            if (heap[child].Priority >= last.Priority)
                break;

            heap[index] = heap[child];
            index = child;
        }
        heap[index] = last;
        return result;
    }

    private static IEnumerable<GridPoint> GetGridPorts(NodeRect node)
    {
        var center = ToGridPoint(node.Center);
        yield return new GridPoint(center.X + 1, center.Y);
        yield return new GridPoint(center.X - 1, center.Y);
        yield return new GridPoint(center.X, center.Y + 1);
        yield return new GridPoint(center.X, center.Y - 1);
    }

    private static bool SegmentBlocked(Vector2 start, Vector2 end, NodeRect source, NodeRect target,
        IEnumerable<NodeRect> obstacles, IReadOnlyList<Vector2> terminals)
    {
        foreach (var obstacle in obstacles)
        {
            if (obstacle == source || obstacle == target)
                continue;
            var inflated = new NodeRect(obstacle.Left - 6f, obstacle.Top - 6f, obstacle.Right + 6f, obstacle.Bottom + 6f);
            if (SegmentIntersects(start, end, inflated))
                return true;
        }
        return IntersectsTerminals(start, end, terminals);
    }

    private static IReadOnlyList<Vector2> ReconstructGridRoute(GridState current,
        IReadOnlyDictionary<GridState, GridState> previous, NodeRect source, NodeRect target)
    {
        var points = new List<Vector2> { ToRoutePoint(current.Point) };
        while (previous.TryGetValue(current, out var parent))
        {
            current = parent;
            points.Add(ToRoutePoint(current.Point));
        }
        points.Reverse();
        if (points.Count == 1)
        {
            var direction = target.Center - source.Center;
            return [GetEdgePoint(source, direction), GetEdgePoint(target, -direction)];
        }
        points.Insert(0, GetEdgePoint(source, points[0] - source.Center));
        points.Add(GetEdgePoint(target, points[^1] - target.Center));
        return SimplifyRoute(points);
    }

    private static GridPoint ToGridPoint(Vector2 point)
        => new((int) MathF.Round((point.X - NodeSize / 2f) / RouteGridStep),
            (int) MathF.Round((point.Y - NodeSize / 2f) / RouteGridStep));

    private static Vector2 ToRoutePoint(GridPoint point)
        => new(point.X * RouteGridStep + NodeSize / 2f, point.Y * RouteGridStep + NodeSize / 2f);

    private static int GridDistance(GridPoint point, IEnumerable<GridPoint> targets)
        => targets.Min(target => Math.Abs(point.X - target.X) + Math.Abs(point.Y - target.Y));

    private static IReadOnlyList<Vector2> SimplifyRoute(IReadOnlyList<Vector2> route)
    {
        var result = new List<Vector2> { route[0] };
        for (var i = 1; i < route.Count - 1; i++)
        {
            var incoming = route[i] - result[^1];
            var outgoing = route[i + 1] - route[i];
            if (MathHelper.CloseTo(incoming.X * outgoing.Y - incoming.Y * outgoing.X, 0f))
                continue;
            result.Add(route[i]);
        }
        result.Add(route[^1]);
        return result;
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

    private static float RouteScore(
        IReadOnlyList<Vector2> route,
        NodeRect source,
        NodeRect target,
        IReadOnlyList<NodeRect> obstacles,
        IReadOnlyList<OccupiedSegment> occupied,
        string sourceId,
        string targetId)
    {
        var score = RouteLength(route) + CountBends(route) * BendPenalty;
        if (IntersectsObstacles(route, source, target, obstacles))
            score += ObstaclePenalty;

        for (var i = 1; i < route.Count; i++)
        {
            var segment = new RouteSegment(route[i - 1], route[i]);
            foreach (var other in occupied)
            {
                if (SegmentsOverlap(segment, other.Segment))
                {
                    var sourceBranch = i == 1 && other.SourceBranch && other.Source == sourceId;
                    var targetBranch = i == route.Count - 1 && other.TargetBranch && other.Target == targetId;
                    score += (sourceBranch || targetBranch ? -0.5f : OverlapPenalty) *
                             SegmentOverlapLength(segment, other.Segment);
                }
                else if (SegmentsCross(segment, other.Segment))
                    score += CrossingPenalty;
            }
        }
        return score;
    }

    private static int CountBends(IReadOnlyList<Vector2> route)
    {
        var bends = 0;
        for (var i = 1; i < route.Count - 1; i++)
        {
            var incoming = route[i] - route[i - 1];
            var outgoing = route[i + 1] - route[i];
            if (incoming.LengthSquared() > 0f && outgoing.LengthSquared() > 0f &&
                !MathHelper.CloseTo(Vector2.Dot(Vector2.Normalize(incoming), Vector2.Normalize(outgoing)), 1f))
                bends++;
        }
        return bends;
    }

    private static void AddSegments(
        IReadOnlyList<Vector2> route,
        string sourceId,
        string targetId,
        ICollection<OccupiedSegment> occupied,
        bool branches = false)
    {
        for (var i = 1; i < route.Count; i++)
        {
            occupied.Add(new OccupiedSegment(
                new RouteSegment(route[i - 1], route[i]),
                sourceId,
                targetId,
                branches && i == 1,
                branches && i == route.Count - 1));
        }
    }

    private static bool SegmentsCross(RouteSegment first, RouteSegment second)
        => TryGetCrossing(first, second, out _);

    private static bool TryGetCrossing(RouteSegment first, RouteSegment second, out Vector2 crossing)
    {
        crossing = default;
        if (first.Start == first.End || second.Start == second.End)
            return false;
        var firstVertical = MathHelper.CloseTo(first.Start.X, first.End.X);
        var secondVertical = MathHelper.CloseTo(second.Start.X, second.End.X);
        if (firstVertical == secondVertical)
            return false;

        var vertical = firstVertical ? first : second;
        var horizontal = firstVertical ? second : first;
        crossing = new Vector2(vertical.Start.X, horizontal.Start.Y);
        return Between(crossing.X, horizontal.Start.X, horizontal.End.X) &&
               Between(crossing.Y, vertical.Start.Y, vertical.End.Y);
    }

    private static bool SegmentsOverlap(RouteSegment first, RouteSegment second)
    {
        var firstVertical = MathHelper.CloseTo(first.Start.X, first.End.X);
        var secondVertical = MathHelper.CloseTo(second.Start.X, second.End.X);
        if (firstVertical != secondVertical)
            return false;
        return firstVertical
            ? MathHelper.CloseTo(first.Start.X, second.Start.X) && RangesOverlap(first.Start.Y, first.End.Y, second.Start.Y, second.End.Y)
            : MathHelper.CloseTo(first.Start.Y, second.Start.Y) && RangesOverlap(first.Start.X, first.End.X, second.Start.X, second.End.X);
    }

    private static float SegmentOverlapLength(RouteSegment first, RouteSegment second)
    {
        return MathHelper.CloseTo(first.Start.X, first.End.X)
            ? RangeOverlapLength(first.Start.Y, first.End.Y, second.Start.Y, second.End.Y)
            : RangeOverlapLength(first.Start.X, first.End.X, second.Start.X, second.End.X);
    }

    private static bool Between(float value, float first, float second) => value > Math.Min(first, second) && value < Math.Max(first, second);

    private static bool RangesOverlap(float firstStart, float firstEnd, float secondStart, float secondEnd)
        => RangeOverlapLength(firstStart, firstEnd, secondStart, secondEnd) > 0f;

    private static float RangeOverlapLength(float firstStart, float firstEnd, float secondStart, float secondEnd)
        => Math.Max(0f, Math.Min(Math.Max(firstStart, firstEnd), Math.Max(secondStart, secondEnd)) -
                         Math.Max(Math.Min(firstStart, firstEnd), Math.Min(secondStart, secondEnd)));

    private static (IReadOnlyList<Vector2> Source, IReadOnlyList<Vector2> Target) CreateLongRoutes(
        FancyResearchConsoleItem sourceItem,
        FancyResearchConsoleItem targetItem,
        NodeRect source,
        NodeRect target,
        IEnumerable<NodeRect> obstacles,
        Dictionary<string, int> stubSlots,
        ICollection<Vector2> terminals,
        IReadOnlyList<OccupiedSegment> occupied)
    {
        var delta = targetItem.Prototype.Position - sourceItem.Prototype.Position;
        var horizontal = Math.Abs(delta.X) >= Math.Abs(delta.Y);
        var direction = horizontal ? Math.Sign(delta.X) : Math.Sign(delta.Y);
        if (direction == 0)
            direction = 1;

        var preferred = (horizontal ? Vector2.UnitX : Vector2.UnitY) * direction;
        var sourceStub = CreateFreeStub(sourceItem.Prototype.ID, source, target, preferred, 1f, obstacles, stubSlots, terminals, occupied);
        terminals.Add(sourceStub[^1]);
        var targetOccupied = occupied.ToList();
        AddSegments(sourceStub, sourceItem.Prototype.ID, targetItem.Prototype.ID, targetOccupied);
        var targetStub = CreateFreeStub(targetItem.Prototype.ID, target, source, -preferred, 1f, obstacles, stubSlots, terminals, targetOccupied);
        terminals.Add(targetStub[^1]);

        return (sourceStub, targetStub);
    }

    private static IReadOnlyList<Vector2> CreateFreeStub(string id, NodeRect source, NodeRect target,
        Vector2 preferred, float scale, IEnumerable<NodeRect> obstacles, Dictionary<string, int> stubSlots,
        IEnumerable<Vector2> terminals, IReadOnlyList<OccupiedSegment> occupied)
    {
        var directions = new[] { preferred, new Vector2(-preferred.Y, preferred.X), new Vector2(preferred.Y, -preferred.X), -preferred };
        IReadOnlyList<Vector2>? fallback = null;
        var fallbackScore = float.MaxValue;
        foreach (var direction in directions)
        {
            var key = $"{id}:{direction.X},{direction.Y}";
            var firstSlot = stubSlots.GetValueOrDefault(key);
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var slot = firstSlot + attempt;
                var start = GetEdgePoint(source, direction, GetPortOffset(slot));
                var length = (StubGridLength + attempt / 4 * StubSlotStep) * 150f * scale;
                var end = start + direction * length;
                var obstacleConflict = IntersectsObstacles([start, end], source, target, obstacles);
                var terminalConflict = IntersectsTerminals(start, end, terminals);
                var routeConflict = ConflictsWithRoutes([start, end], occupied);
                var clearance = HasTerminalClearance(end, terminals);
                var terminalFits = TerminalFits(end, source, target, obstacles);
                var terminalRouteConflict = TerminalIntersectsRoutes(end, occupied);
                if (!obstacleConflict && !terminalConflict && !routeConflict && clearance && terminalFits && !terminalRouteConflict)
                {
                    stubSlots[key] = slot + 1;
                    return [start, end];
                }

                if (obstacleConflict || terminalConflict || !clearance || !terminalFits || terminalRouteConflict)
                    continue;
                var score = length +
                            (routeConflict ? CrossingPenalty : 0f);
                if (score < fallbackScore)
                {
                    fallback = [start, end];
                    fallbackScore = score;
                }
            }
        }

        return fallback ?? [source.Center];
    }

    private static bool HasTerminalClearance(Vector2 terminal, IEnumerable<Vector2> terminals)
        => terminals.All(existing => Vector2.DistanceSquared(existing, terminal) >= TerminalClearance * TerminalClearance);

    private static bool TerminalFits(Vector2 terminal, NodeRect source, NodeRect target, IEnumerable<NodeRect> obstacles)
    {
        var halfSize = TerminalSize / 2f + 2f;
        var terminalBounds = new NodeRect(terminal.X - halfSize, terminal.Y - halfSize,
            terminal.X + halfSize, terminal.Y + halfSize);
        return obstacles.All(obstacle => obstacle == source || obstacle == target || !RectsOverlap(terminalBounds, obstacle));
    }

    private static bool TerminalIntersectsRoutes(Vector2 terminal, IReadOnlyList<OccupiedSegment> occupied)
    {
        var halfSize = TerminalSize / 2f + 2f;
        var bounds = new NodeRect(terminal.X - halfSize, terminal.Y - halfSize,
            terminal.X + halfSize, terminal.Y + halfSize);
        return occupied.Any(segment => SegmentIntersects(segment.Segment.Start, segment.Segment.End, bounds));
    }

    private static bool RectsOverlap(NodeRect first, NodeRect second)
        => first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;

    private static float GetPortOffset(int slot)
    {
        if (slot == 0)
            return 0f;
        var row = (slot + 1) / 2;
        return row * TerminalPortSpacing * (slot % 2 == 0 ? -1f : 1f);
    }

    private static bool IntersectsTerminals(Vector2 start, Vector2 end, IEnumerable<Vector2> terminals)
    {
        var halfSize = TerminalSize / 2f + 2f;
        foreach (var terminal in terminals)
        {
            var bounds = new NodeRect(terminal.X - halfSize, terminal.Y - halfSize,
                terminal.X + halfSize, terminal.Y + halfSize);
            if (SegmentIntersects(start, end, bounds))
                return true;
        }
        return false;
    }

    private static bool IntersectsTerminals(IReadOnlyList<Vector2> route, IReadOnlyList<Vector2> terminals)
    {
        for (var i = 1; i < route.Count; i++)
        {
            if (IntersectsTerminals(route[i - 1], route[i], terminals))
                return true;
        }
        return false;
    }

    private static bool ConflictsWithRoutes(IReadOnlyList<Vector2> route, IReadOnlyList<OccupiedSegment> occupied)
    {
        for (var i = 1; i < route.Count; i++)
        {
            var segment = new RouteSegment(route[i - 1], route[i]);
            if (occupied.Any(other => SegmentsCross(segment, other.Segment) || SegmentsOverlap(segment, other.Segment)))
                return true;
        }
        return false;
    }

    private static Vector2 GetEdgePoint(NodeRect source, Vector2 direction, float offset = 0f)
    {
        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
            return new Vector2(direction.X > 0 ? source.Right : source.Left,
                Math.Clamp(source.Center.Y + offset, source.Top + 8f, source.Bottom - 8f));
        return new Vector2(Math.Clamp(source.Center.X + offset, source.Left + 8f, source.Right - 8f),
            direction.Y > 0 ? source.Bottom : source.Top);
    }

    private static void DrawPortalIcon(DrawingHandleScreen handle, Vector2 center, float scale, Texture? texture, Color borderColor)
    {
        var size = new Vector2(24f) * scale;
        var bounds = UIBox2.FromDimensions(center - size / 2f, size);
        handle.DrawRect(bounds, TerminalBackground);
        handle.DrawRect(bounds, borderColor, false);
        if (texture != null)
            handle.DrawTextureRect(texture, new UIBox2(bounds.Left + 4f * scale, bounds.Top + 4f * scale,
                bounds.Right - 4f * scale, bounds.Bottom - 4f * scale));
    }

    private static void DrawConnection(
        DrawingHandleScreen handle,
        RoutedConnection connection,
        Vector2 origin,
        float scale,
        Color color)
    {
        DrawRoute(handle, connection.SourceRoute, connection.SourceBridges, origin, scale, color);
        if (connection.TargetRoute == null)
            return;

        DrawRoute(handle, connection.TargetRoute, connection.TargetBridges, origin, scale, color);
        DrawPortalIcon(handle, origin + connection.SourceRoute[^1] * scale, scale, connection.Target.ResearchTexture, color);
        DrawPortalIcon(handle, origin + connection.TargetRoute[^1] * scale, scale, connection.Source.ResearchTexture, color);
    }

    private static NodeRect GetTreeBounds(FancyResearchConsoleItem item)
    {
        return new NodeRect(item.TreePosition.X, item.TreePosition.Y,
            item.TreePosition.X + NodeSize, item.TreePosition.Y + NodeSize);
    }

    private static void DrawRoute(
        DrawingHandleScreen handle,
        IReadOnlyList<Vector2> route,
        IReadOnlyList<RouteBridge> bridges,
        Vector2 origin,
        float scale,
        Color color)
    {
        for (var i = 1; i < route.Count; i++)
        {
            var start = origin + route[i - 1] * scale;
            var end = origin + route[i] * scale;
            DrawBridgedSegment(handle, start, end, bridges, i, origin, scale, color);
        }
    }

    private static void DrawBridgedSegment(
        DrawingHandleScreen handle,
        Vector2 start,
        Vector2 end,
        IReadOnlyList<RouteBridge> bridges,
        int segment,
        Vector2 origin,
        float scale,
        Color color)
    {
        var hasBridges = false;
        foreach (var bridge in bridges)
        {
            if (bridge.Segment != segment)
                continue;

            hasBridges = true;
            break;
        }

        if (!hasBridges)
        {
            handle.DrawLine(start, end, color);
            return;
        }

        var direction = Vector2.Normalize(end - start);
        var normal = new Vector2(-direction.Y, direction.X);
        var halfGap = BridgeHalfGap * scale;
        var cap = normal * BridgeCapHalfLength * scale;
        var cursor = start;
        foreach (var bridge in bridges)
        {
            if (bridge.Segment != segment)
                continue;

            var position = origin + bridge.Position * scale;
            var before = position - direction * halfGap;
            var after = position + direction * halfGap;
            handle.DrawLine(cursor, before, color);
            handle.DrawLine(before - cap, before + cap, color);
            handle.DrawLine(after - cap, after + cap, color);
            cursor = after;
        }
        handle.DrawLine(cursor, end, color);
    }

    private static float RouteLength(IReadOnlyList<Vector2> route)
    {
        var length = 0f;
        for (var i = 1; i < route.Count; i++)
            length += Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y);
        return length;
    }
}

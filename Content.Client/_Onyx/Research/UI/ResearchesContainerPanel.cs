using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Research.UI;

public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    private static readonly Color ConnectionColor = Color.FromHex("#A8B4C4").WithAlpha(0.7f);

    private readonly record struct Route(Vector2 Start, Vector2 First, Vector2 Second, Vector2 End)
    {
        public float Length => Distance(Start, First) + Distance(First, Second) + Distance(Second, End);
    }

    private readonly record struct NodeRect(float Left, float Top, float Right, float Bottom)
    {
        public Vector2 Center => new((Left + Right) / 2f, (Top + Bottom) / 2f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var items = Children.OfType<FancyResearchConsoleItem>().ToArray();
        var byId = items.ToDictionary(item => item.Prototype.ID);

        foreach (var item in items)
        {
            foreach (var prerequisiteId in item.Prototype.TechnologyPrerequisites)
            {
                if (!byId.TryGetValue(prerequisiteId, out var prerequisite))
                    continue;

                DrawRoute(handle, SelectRoute(GetBounds(prerequisite), GetBounds(item)));
            }
        }
    }

    private static Route SelectRoute(NodeRect source, NodeRect target)
    {
        var horizontal = CreateHorizontalRoute(source, target);
        var vertical = CreateVerticalRoute(source, target);
        return horizontal.Length <= vertical.Length ? horizontal : vertical;
    }

    private static Route CreateHorizontalRoute(NodeRect source, NodeRect target)
    {
        var leftToRight = source.Center.X <= target.Center.X;
        var start = new Vector2(leftToRight ? source.Right : source.Left, source.Center.Y);
        var end = new Vector2(leftToRight ? target.Left : target.Right, target.Center.Y);
        var corridorX = (start.X + end.X) / 2f;
        return new Route(start, new Vector2(corridorX, start.Y), new Vector2(corridorX, end.Y), end);
    }

    private static Route CreateVerticalRoute(NodeRect source, NodeRect target)
    {
        var topToBottom = source.Center.Y <= target.Center.Y;
        var start = new Vector2(source.Center.X, topToBottom ? source.Bottom : source.Top);
        var end = new Vector2(target.Center.X, topToBottom ? target.Top : target.Bottom);
        var corridorY = (start.Y + end.Y) / 2f;
        return new Route(start, new Vector2(start.X, corridorY), new Vector2(end.X, corridorY), end);
    }

    private static NodeRect GetBounds(Control control)
    {
        return new NodeRect(control.PixelPosition.X, control.PixelPosition.Y,
            control.PixelPosition.X + control.PixelWidth, control.PixelPosition.Y + control.PixelHeight);
    }

    private static void DrawRoute(DrawingHandleScreen handle, Route route)
    {
        handle.DrawLine(route.Start, route.First, ConnectionColor);
        handle.DrawLine(route.First, route.Second, ConnectionColor);
        handle.DrawLine(route.Second, route.End, ConnectionColor);
    }

    private static float Distance(Vector2 start, Vector2 end)
    {
        return Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y);
    }
}

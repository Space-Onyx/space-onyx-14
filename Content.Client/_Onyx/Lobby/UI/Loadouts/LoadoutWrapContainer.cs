using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Onyx.Lobby.UI.Loadouts;

/// <summary>Arranges child controls left-to-right and wraps at actual available width.</summary>
public sealed class LoadoutWrapContainer : Container
{
    private const int DefaultSeparation = 6;
    private float _arrangedWidth;

    public int Separation { get; set; } = DefaultSeparation;

    private float ResolveWidth(float available, float widest)
    {
        if (_arrangedWidth > 0)
            return float.IsPositiveInfinity(available) ? _arrangedWidth : Math.Min(available, _arrangedWidth);

        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent.Size.X > 0)
                return float.IsPositiveInfinity(available) ? parent.Size.X : Math.Min(available, parent.Size.X);
        }

        return widest;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var children = Children.Where(child => child.Visible).ToList();
        if (children.Count == 0)
            return Vector2.Zero;

        foreach (var child in children)
            child.Measure(new Vector2(availableSize.X, float.PositiveInfinity));

        var widest = children.Max(child => child.DesiredSize.X);
        var width = Math.Max(ResolveWidth(availableSize.X, widest), widest);
        var height = Layout(children, width, null);
        return new Vector2(float.IsPositiveInfinity(availableSize.X) ? width : availableSize.X, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var children = Children.Where(child => child.Visible).ToList();
        Layout(children, finalSize.X, (child, x, y) => child.Arrange(UIBox2.FromDimensions(x, y, child.DesiredSize.X, child.DesiredSize.Y)));

        if (!MathHelper.CloseTo(_arrangedWidth, finalSize.X, 0.5f))
        {
            _arrangedWidth = finalSize.X;
            InvalidateMeasure();
        }

        return finalSize;
    }

    private float Layout(List<Control> children, float width, Action<Control, float, float>? arrange)
    {
        float x = 0, y = 0, rowHeight = 0;
        var first = true;
        foreach (var child in children)
        {
            var childWidth = child.DesiredSize.X;
            if (!first && x + Separation + childWidth > width)
            {
                y += rowHeight + Separation;
                x = 0;
                rowHeight = 0;
                first = true;
            }

            if (!first)
                x += Separation;
            first = false;
            arrange?.Invoke(child, x, y);
            x += childWidth;
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Y);
        }

        return y + rowHeight;
    }
}

using System.Numerics;
using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Onyx.Lobby.UI.Roles;

public sealed class JobGridContainer : BoxContainer
{
    private const int ColumnCount = 2;
    private const int Separation = 7;
    private readonly List<Control> _cards = new();
    private readonly List<JobGridRow> _rows = new();

    public JobGridContainer()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = Separation;
        HorizontalExpand = true;
        RectClipContent = true;
    }

    public void AddCard(Control card)
    {
        _cards.Add(card);
        Rebuild();
    }

    public void Rebuild()
    {
        foreach (var row in _rows)
        {
            row.RemoveAllChildren();
            RemoveChild(row);
            row.Dispose();
        }

        _rows.Clear();
        var visible = _cards.Where(card => card.Visible).ToArray();
        for (var i = 0; i < visible.Length; i += ColumnCount)
        {
            var row = new JobGridRow(Separation);
            row.AddChild(visible[i]);
            if (i + 1 < visible.Length)
                row.AddChild(visible[i + 1]);
            _rows.Add(row);
            AddChild(row);
        }
    }
}

public sealed class JobGridRow(int separation) : Container
{
    public JobGridRow() : this(7)
    {
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? Parent?.Size.X ?? 0 : availableSize.X;
        var cellWidth = Math.Max(0, (width - separation) / 2);
        float height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Vector2(cellWidth, availableSize.Y));
            height = Math.Max(height, child.DesiredSize.Y);
        }

        return new Vector2(width, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var cellWidth = Math.Max(0, (finalSize.X - separation) / 2);
        var index = 0;
        foreach (var child in Children)
        {
            child.Measure(new Vector2(cellWidth, float.PositiveInfinity));
            child.Arrange(UIBox2.FromDimensions(index * (cellWidth + separation), 0, cellWidth, finalSize.Y));
            index++;
        }

        return finalSize;
    }
}

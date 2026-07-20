using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Research.UI;

public sealed partial class ResearchesContainerPanel : LayoutContainer
{
    protected override void Draw(DrawingHandleScreen handle)
    {
        foreach (var item in Children.OfType<FancyResearchConsoleItem>())
        {
            foreach (var prerequisite in Children.OfType<FancyResearchConsoleItem>()
                         .Where(other => item.Prototype.TechnologyPrerequisites.Contains(other.Prototype.ID)))
            {
                var start = new Vector2(item.PixelPosition.X + item.PixelWidth / 2, item.PixelPosition.Y + item.PixelHeight / 2);
                var end = new Vector2(prerequisite.PixelPosition.X + prerequisite.PixelWidth / 2, prerequisite.PixelPosition.Y + prerequisite.PixelHeight / 2);
                handle.DrawLine(start, new Vector2(end.X, start.Y), Color.White);
                handle.DrawLine(new Vector2(end.X, start.Y), end, Color.White);
            }
        }
    }
}

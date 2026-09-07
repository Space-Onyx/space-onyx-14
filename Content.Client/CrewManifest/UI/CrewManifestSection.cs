using Content.Shared.CrewManifest;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics; // <Onyx-CrewManifest>
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Shared.Roles;

namespace Content.Client.CrewManifest.UI;

public sealed class CrewManifestSection : BoxContainer
{
    public CrewManifestSection(
        IPrototypeManager prototypeManager,
        SpriteSystem spriteSystem,
        DepartmentPrototype section,
        // <Onyx-CrewManifest-edited>
        List<CrewManifestEntry> entries,
        bool stripedRows = false)
        // </Onyx-CrewManifest-edited>
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        AddChild(new Label()
        {
            StyleClasses = { "LabelBig" },
            Text = Loc.GetString(section.Name)
        });

        var gridContainer = new GridContainer()
        {
            HorizontalExpand = true,
            Columns = 2
        };

        if (!stripedRows)
            AddChild(gridContainer); // <Onyx-CrewManifest-edited>

        var rowIndex = 0; // <Onyx-CrewManifest>
        foreach (var entry in entries)
        {
            var name = new RichTextLabel()
            {
                HorizontalExpand = true,
            };
            name.SetMessage(entry.Name);

            var titleContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };

            var title = new RichTextLabel();
            title.SetMessage(entry.JobTitle);


            if (prototypeManager.TryIndex<JobIconPrototype>(entry.JobIcon, out var jobIcon))
            {
                var icon = new TextureRect()
                {
                    TextureScale = new Vector2(2, 2),
                    VerticalAlignment = VAlignment.Center,
                    Texture = spriteSystem.Frame0(jobIcon.Icon),
                    Margin = new Thickness(0, 0, 4, 0)
                };

                titleContainer.AddChild(icon);
                titleContainer.AddChild(title);
            }
            else
            {
                titleContainer.AddChild(title);
            }

            // <Onyx-CrewManifest-edited>
            if (stripedRows)
            {
                var rowBackground = new StyleBoxFlat
                {
                    BackgroundColor = rowIndex++ % 2 == 0
                        ? Color.FromHex("#1B222B")
                        : Color.FromHex("#2B3542"),
                    ContentMarginLeftOverride = 8,
                    ContentMarginTopOverride = 5,
                    ContentMarginRightOverride = 8,
                    ContentMarginBottomOverride = 5,
                };

                var row = new GridContainer
                {
                    Columns = 2,
                    HorizontalExpand = true,
                };
                row.AddChild(name);
                row.AddChild(titleContainer);

                var rowPanel = new PanelContainer
                {
                    PanelOverride = rowBackground,
                    HorizontalExpand = true,
                    Children = { row },
                };
                AddChild(rowPanel);
            }
            else
            {
                gridContainer.AddChild(name);
                gridContainer.AddChild(titleContainer);
            }
            // </Onyx-CrewManifest-edited>
        }
    }
}

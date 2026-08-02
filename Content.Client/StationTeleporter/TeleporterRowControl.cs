using Content.Client.Message;
using Content.Shared.StationTeleporter;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.StationTeleporter;

/// <summary>
/// A single row in <see cref="StationTeleporterConsoleWindow"/>'s teleporter list: the name label plus the
/// locate/link buttons for one <see cref="StationTeleporterStatus"/>.
/// </summary>
public sealed class TeleporterRowControl : PanelContainer
{
    private static readonly Color LinkedBackgroundColor = new(18, 61, 82); //TODO: UI palette usage
    private static readonly Color UnlinkedBackgroundColor = new(30, 30, 34); //TODO: UI palette usage
    private static readonly Color SelectedBackgroundColor = new(49, 117, 7); //TODO: UI palette usage

    public readonly NetEntity TeleporterUid;
    public readonly EntityCoordinates? Coordinates;
    public readonly TeleporterButton LocateButton;
    public readonly TeleporterButton LinkButton;

    public TeleporterRowControl(StationTeleporterStatus teleporter, bool selected, EntityCoordinates? coordinates)
    {
        TeleporterUid = teleporter.TeleporterUid;
        Coordinates = coordinates;

        var linked = teleporter.LinkCoordinates is not null;

        var bgColor = linked ? LinkedBackgroundColor : UnlinkedBackgroundColor;
        if (selected)
            bgColor = SelectedBackgroundColor;

        // <Onyx-StationTeleporterUI-edited>
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;
        HorizontalExpand = true;
        Margin = new Thickness(4, 2, 4, 2);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = bgColor,
            BorderColor = Color.Black,
            BorderThickness = new(2),
        };

        var mainBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        AddChild(mainBox);

        // Teleporter name
        var nameLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Left,
            MaxWidth = 280f,
            Margin = new Thickness(10, 8, 10, 0),
        };
        nameLabel.SetMarkup($"[bold]{teleporter.Name}[/bold]");
        mainBox.AddChild(nameLabel);

        var statusLabel = new Label
        {
            Text = Loc.GetString(!teleporter.Powered
                ? "teleporter-console-user-interface-status-no-power"
                : linked
                    ? "teleporter-console-user-interface-status-linked"
                    : "teleporter-console-user-interface-status-ready"),
            Margin = new Thickness(10, 2, 10, 6),
        };
        statusLabel.AddStyleClass("LabelSubText");
        mainBox.AddChild(statusLabel);

        var buttonBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(10, 0, 10, 10),
        };
        mainBox.AddChild(buttonBox);

        // Locating button
        LocateButton = new TeleporterButton
        {
            Text = Loc.GetString("teleporter-console-user-interface-locate"),
            TeleporterUid = teleporter.TeleporterUid,
            Coordinates = coordinates,
            HorizontalExpand = true,
        };
        buttonBox.AddChild(LocateButton);

        // Link/Unlink button
        var buttonLoc = "teleporter-console-user-interface-start-connection";
        if (!teleporter.Powered)
            buttonLoc = "teleporter-console-user-interface-no-power";
        else if (linked)
            buttonLoc = "teleporter-console-user-interface-cut-connection";

        LinkButton = new TeleporterButton
        {
            Text = Loc.GetString(buttonLoc),
            TeleporterUid = teleporter.TeleporterUid,
            Coordinates = coordinates,
            HorizontalExpand = true,
            Disabled = !teleporter.Powered,
        };
        buttonBox.AddChild(LinkButton);
        // </Onyx-StationTeleporterUI-edited>
    }
}

public sealed class TeleporterButton : Button
{
    public int IndexInTable;
    public NetEntity TeleporterUid;
    public EntityCoordinates? Coordinates;
}

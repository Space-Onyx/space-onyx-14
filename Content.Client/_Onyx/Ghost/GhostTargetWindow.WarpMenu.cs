// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Onyx.Ghost;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed partial class GhostTargetWindow
{
    [Dependency] private IPrototypeManager _ghostWarpPrototypes = default!;
    [Dependency] private IEntitySystemManager _ghostWarpSystems = default!;

    private List<GhostWarpMenuEntry> _ghostWarpEntries = [];
    private bool _useGhostWarpMenu;

    public void PrepareGhostWarpMenu()
    {
        _useGhostWarpMenu = true;
        MinSize = new Vector2(950, 550);
        SetSize = new Vector2(950, 550);
        PopulateGhostWarpMenu();
    }

    public void UpdateWarpMenu(IEnumerable<GhostWarpMenuEntry> entries)
    {
        _ghostWarpEntries = entries.ToList();
        PopulateGhostWarpMenu();
    }

    private void PopulateGhostWarpMenu()
    {
        if (!_useGhostWarpMenu)
            return;

        ButtonContainer.RemoveAllChildren();

        AddGhostWarpSection(GhostWarpMenuCategory.Antagonist, "ghost-teleport-menu-antagonists-label");
        AddGhostWarpSection(GhostWarpMenuCategory.Alive, "ghost-teleport-menu-alive-label");
        AddGhostWarpSection(GhostWarpMenuCategory.Ghost, "ghost-teleport-menu-ghosts-label");
        AddGhostWarpSection(GhostWarpMenuCategory.Disconnected, "ghost-teleport-menu-left-label");
        AddGhostWarpSection(GhostWarpMenuCategory.Dead, "ghost-teleport-menu-dead-label");
        AddGhostWarpSection(GhostWarpMenuCategory.Location, "ghost-teleport-menu-locations-label");
    }

    private void AddGhostWarpSection(GhostWarpMenuCategory category, string title)
    {
        var entries = _ghostWarpEntries
            .Where(entry => entry.Category == category && MatchesGhostWarpSearch(entry))
            .ToList();

        if (entries.Count == 0)
            return;

        var section = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 6),
        };

        var header = new StripeBack();
        header.AddChild(new Label
        {
            Text = Loc.GetString(title),
            StyleClasses = { "LabelBig" },
            Align = Label.AlignMode.Center,
        });
        section.AddChild(header);

        foreach (var group in entries.GroupBy(entry => entry.Group).OrderByDescending(GetGhostWarpGroupWeight))
        {
            var groupEntries = group.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var groupColor = GetGhostWarpGroupColor(category, group.Key);
            var groupName = GetGhostWarpGroupName(category, group.Key);

            section.AddChild(new Label
            {
                Text = Loc.GetString("ghost-teleport-menu-group-count", ("group", groupName), ("count", groupEntries.Count)),
                StyleClasses = { "LabelSecondaryColor" },
                FontColorOverride = groupColor,
                Margin = new Thickness(2, 2, 0, 0),
            });

            var grid = new GridContainer
            {
                Columns = 5,
                HorizontalExpand = true,
            };

            foreach (var entry in groupEntries)
                grid.AddChild(CreateGhostWarpButton(entry, GetGhostWarpButtonColor(entry.Category, groupColor)));

            section.AddChild(grid);
        }

        ButtonContainer.AddChild(section);
    }

    private Button CreateGhostWarpButton(GhostWarpMenuEntry entry, Color color)
    {
        var button = new Button
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SetWidth = 180,
            SetHeight = 36,
            ToolTip = entry.Name,
            TooltipDelay = 0.1f,
            ModulateSelfOverride = color,
            StyleClasses = { "OpenBoth" },
        };
        button.OnPressed += _ => WarpClicked?.Invoke(entry.Entity);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        if (entry.JobIcon is { } iconId && _ghostWarpPrototypes.TryIndex<JobIconPrototype>(iconId, out var icon))
        {
            row.AddChild(new TextureRect
            {
                Texture = _ghostWarpSystems.GetEntitySystem<SpriteSystem>().Frame0(icon.Icon),
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
            });
        }

        row.AddChild(new Label
        {
            Text = entry.Name,
            ClipText = true,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        });
        button.AddChild(row);
        return button;
    }

    private bool MatchesGhostWarpSearch(GhostWarpMenuEntry entry)
    {
        return string.IsNullOrWhiteSpace(_searchText)
            || entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || entry.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true
            || entry.Group?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    private int GetGhostWarpGroupWeight(IGrouping<string?, GhostWarpMenuEntry> group)
    {
        return group.Key != null && _ghostWarpPrototypes.TryIndex<DepartmentPrototype>(group.Key, out var department)
            ? department.Weight
            : 0;
    }

    private string GetGhostWarpGroupName(GhostWarpMenuCategory category, string? group)
    {
        if (category == GhostWarpMenuCategory.Location)
            return Loc.GetString("ghost-teleport-menu-count-label");

        if (group != null && _ghostWarpPrototypes.TryIndex<DepartmentPrototype>(group, out var department))
            return Loc.GetString(department.Name);

        return group ?? Loc.GetString("generic-unknown-title");
    }

    private Color GetGhostWarpGroupColor(GhostWarpMenuCategory category, string? group)
    {
        if (category == GhostWarpMenuCategory.Antagonist)
            return Color.FromHex("#8f3535");

        if (group != null && _ghostWarpPrototypes.TryIndex<DepartmentPrototype>(group, out var department))
            return department.Color;

        return Color.FromHex("#666a73");
    }

    private static Color GetGhostWarpButtonColor(GhostWarpMenuCategory category, Color groupColor)
    {
        if (category == GhostWarpMenuCategory.Location)
            return groupColor;

        if (category is GhostWarpMenuCategory.Dead or GhostWarpMenuCategory.Disconnected)
            return Color.FromHex("#454851");

        return Color.InterpolateBetween(Color.Black, groupColor, 0.97f);
    }
}

// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Components;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Research.UI;

public static class ResearchExperimentUiHelpers
{
    public static Control BuildEntry(ResearchExperimentUiEntry experiment)
    {
        var wrappingLabels = new List<RichTextLabel>();
        var (status, accentColor) = experiment.Status switch
        {
            ResearchExperimentUiStatus.Active => ("research-experiment-ui-status-active", Color.FromHex("#62B8D8")),
            ResearchExperimentUiStatus.UnsupportedSource => ("research-experiment-ui-status-other-scanner", Color.FromHex("#D69A45")),
            ResearchExperimentUiStatus.Completed => ("research-experiment-ui-status-completed", Color.FromHex("#59B66D")),
            _ => ("research-experiment-ui-status-locked", Color.FromHex("#68717E")),
        };

        var title = new RichTextLabel { HorizontalExpand = true };
        wrappingLabels.Add(title);
        title.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[bold]{FormattedMessage.EscapeText(experiment.Name)}[/bold]"));
        var statusRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 5,
            Children =
            {
                new PanelContainer
                {
                    MinWidth = 7,
                    MaxWidth = 7,
                    MinHeight = 7,
                    MaxHeight = 7,
                    PanelOverride = new StyleBoxFlat { BackgroundColor = accentColor },
                },
                new Label
                {
                    Text = Loc.GetString(status),
                    Modulate = Color.FromHex("#AEB7C4"),
                    StyleClasses = { StyleClass.FontSmall },
                },
            },
        };
        var heading = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            Children = { title, statusRow },
        };
        var description = new RichTextLabel { HorizontalExpand = true };
        wrappingLabels.Add(description);
        description.SetMessage(FormattedMessage.FromUnformatted(experiment.Description));

        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(0, 2),
        };
        AddDetail(details, wrappingLabels, "research-experiment-ui-source-label", experiment.Source);
        AddDetail(details, wrappingLabels, "research-experiment-ui-technology-label", experiment.RequiredTechnologies);
        AddDetail(details, wrappingLabels, "research-experiment-ui-reward-label", experiment.Reward);

        var tasks = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        foreach (var task in experiment.Tasks)
        {
            var taskLabel = new RichTextLabel { HorizontalExpand = true };
            taskLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color=#D5DAE1]{FormattedMessage.EscapeText(task.Goal)}[/color]  " +
                $"[color={accentColor.ToHex()}]{task.Progress}/{task.Target}[/color]"));
            tasks.AddChild(taskLabel);
            wrappingLabels.Add(taskLabel);
        }
        tasks.Visible = experiment.Status is ResearchExperimentUiStatus.Active or ResearchExperimentUiStatus.UnsupportedSource;

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10),
            Children = { heading, description, details, tasks },
        };
        content.OnResized += () =>
        {
            var width = content.Size.X;
            if (width <= 8f)
                return;

            foreach (var label in wrappingLabels)
            {
                if (!MathHelper.CloseTo(label.SetWidth, width))
                    label.SetWidth = width;
            }
        };

        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1E2229"),
                BorderColor = accentColor.WithAlpha(0.65f),
                BorderThickness = new Thickness(2, 1, 1, 1),
            },
            Children =
            {
                content,
            },
        };
    }

    private static void AddDetail(
        BoxContainer details,
        List<RichTextLabel> wrappingLabels,
        string labelKey,
        string value)
    {
        var valueLabel = new RichTextLabel { HorizontalExpand = true };
        valueLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color=#7F8996]{FormattedMessage.EscapeText(Loc.GetString(labelKey))}:[/color] " +
            $"[color=#C7CDD6]{FormattedMessage.EscapeText(value)}[/color]"));
        details.AddChild(valueLabel);
        wrappingLabels.Add(valueLabel);
    }

    public static void Fill(BoxContainer container, IReadOnlyList<ResearchExperimentUiEntry> experiments)
    {
        container.RemoveAllChildren();
        foreach (var experiment in experiments)
            container.AddChild(BuildEntry(experiment));
        if (experiments.Count == 0)
            container.AddChild(new Label { Text = Loc.GetString("research-experiment-ui-empty"), Modulate = Color.Gray });
    }

    public static void SetResult(RichTextLabel label, string result) =>
        label.SetMessage(FormattedMessage.FromUnformatted(string.IsNullOrWhiteSpace(result)
            ? Loc.GetString("research-machine-common-none")
            : result));
}

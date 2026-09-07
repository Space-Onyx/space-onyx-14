// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Research.Components;
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
        var title = new RichTextLabel { HorizontalExpand = true };
        wrappingLabels.Add(title);
        var (color, status) = experiment.Status switch
        {
            ResearchExperimentUiStatus.Active => ("lightblue", "research-experiment-ui-status-active"),
            ResearchExperimentUiStatus.Completed => ("limegreen", "research-experiment-ui-status-completed"),
            _ => ("gray", "research-experiment-ui-status-locked"),
        };
        title.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={color}]{FormattedMessage.EscapeText(experiment.Name)}[/color]  [color={color}]{FormattedMessage.EscapeText(Loc.GetString(status))}[/color]"));
        var description = new RichTextLabel { HorizontalExpand = true };
        wrappingLabels.Add(description);
        description.SetMessage(FormattedMessage.FromUnformatted(experiment.Description));
        var tasks = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        foreach (var task in experiment.Tasks)
        {
            var taskLabel = new RichTextLabel { HorizontalExpand = true };
            taskLabel.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("research-experiment-ui-task",
                ("goal", task.Goal), ("progress", task.Progress), ("target", task.Target))));
            tasks.AddChild(taskLabel);
            wrappingLabels.Add(taskLabel);
        }
        tasks.Visible = experiment.Status != ResearchExperimentUiStatus.Locked;

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            Margin = new Thickness(8),
            Children = { title, description, tasks },
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
                BackgroundColor = Color.FromHex("#20232A"),
                BorderColor = Color.FromHex("#454B57"),
                BorderThickness = new Thickness(1),
            },
            Children =
            {
                content,
            },
        };
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

using System.Text;
using Content.Client._Onyx.HealthExaminable;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.Examine;

public sealed partial class ExamineSystem
{
    private const float PartStatusMaxWidth = 480f;
    private const float PartStatusMarkerWidth = 12f;
    private const float PartStatusListMaxHeight = 360f;

    private bool TryAddPartStatusMessage(Control parent, FormattedMessage message)
    {
        var hasPartStatus = false;
        foreach (var node in message.Nodes)
        {
            if (node.Name != "partstatus")
                continue;

            hasPartStatus = true;
            break;
        }

        if (!hasPartStatus)
            return false;

        var segment = new StringBuilder();
        var skipChatCopy = false;
        BoxContainer? statusList = null;
        foreach (var node in message.Nodes)
        {
            if (node.Name == "partstatusend")
            {
                skipChatCopy = false;
                continue;
            }

            if (skipChatCopy)
                continue;

            if (node.Name != "partstatus")
            {
                segment.Append(node);
                continue;
            }

            AddTextSegment(parent, segment);
            if (PartStatusTag.TryRead(node, out var summary, out var severity, out var details))
            {
                if (statusList == null)
                {
                    statusList = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        SeparationOverride = 2,
                        MaxWidth = PartStatusMaxWidth,
                        HorizontalExpand = true,
                    };
                    parent.AddChild(new ScrollContainer
                    {
                        MaxWidth = PartStatusMaxWidth,
                        MaxHeight = PartStatusListMaxHeight,
                        ReturnMeasure = true,
                        HScrollEnabled = false,
                        HorizontalExpand = true,
                        Children = { statusList },
                    });
                }

                statusList.AddChild(CreatePartStatus(summary, severity, details));
            }
            skipChatCopy = true;
        }

        AddTextSegment(parent, segment);
        parent.AddChild(new Control { MinHeight = 8 });
        return true;
    }

    private static void AddTextSegment(Control parent, StringBuilder segment)
    {
        var markup = segment.ToString();
        segment.Clear();
        if (string.IsNullOrWhiteSpace(FormattedMessage.RemoveMarkupPermissive(markup)))
            return;

        var label = new RichTextLabel
        {
            Margin = new Thickness(4, 4, 0, 0),
            MaxWidth = PartStatusMaxWidth,
        };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(markup),
        [
            typeof(BoldItalicTag),
            typeof(BoldTag),
            typeof(BulletTag),
            typeof(ColorTag),
            typeof(FontTag),
            typeof(HeadingTag),
            typeof(ItalicTag),
        ]);
        parent.AddChild(label);
    }

    private static Control CreatePartStatus(string summary, string severity, string details)
    {
        var hasDetails = !string.IsNullOrWhiteSpace(details);
        var arrow = new Label
        {
            Text = hasDetails ? "▸" : "•",
            SetWidth = PartStatusMarkerWidth,
            FontColorOverride = SeverityColor(severity),
            VerticalAlignment = Control.VAlignment.Top,
        };
        var summaryLabel = new RichTextLabel
        {
            MaxWidth = PartStatusMaxWidth - PartStatusMarkerWidth - 10f,
            HorizontalExpand = true,
        };
        summaryLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(summary));

        var heading = new ContainerButton
        {
            MouseFilter = hasDetails ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore,
            MaxWidth = PartStatusMaxWidth,
            HorizontalExpand = true,
            HorizontalAlignment = Control.HAlignment.Stretch,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                ContentMarginLeftOverride = 4,
                ContentMarginTopOverride = 2,
                ContentMarginRightOverride = 4,
                ContentMarginBottomOverride = 2,
            },
        };
        var headingRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 1,
            MaxWidth = PartStatusMaxWidth,
            HorizontalExpand = true,
        };
        headingRow.AddChild(arrow);
        headingRow.AddChild(summaryLabel);
        heading.AddChild(headingRow);

        var detailLabel = new RichTextLabel
        {
            Margin = new Thickness(18, 1, 4, 3),
            Visible = false,
            MaxWidth = PartStatusMaxWidth - 22f,
            HorizontalExpand = true,
        };
        detailLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(details));

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MaxWidth = PartStatusMaxWidth,
            HorizontalExpand = true,
            HorizontalAlignment = Control.HAlignment.Stretch,
        };
        container.AddChild(heading);
        container.AddChild(detailLabel);

        var entry = new PanelContainer
        {
            MaxWidth = PartStatusMaxWidth,
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#11151A40"),
                BorderColor = Color.FromHex("#59616D70"),
                BorderThickness = new Thickness(0, 0, 0, 1),
            },
            Children = { container },
        };

        if (hasDetails)
        {
            heading.OnPressed += _ =>
            {
                detailLabel.Visible = !detailLabel.Visible;
                arrow.Text = detailLabel.Visible ? "▾" : "▸";
                InvalidateParents(entry);
            };
        }

        return entry;
    }

    private static Color SeverityColor(string severity) => severity switch
    {
        "minor" => Color.Yellow,
        "moderate" => Color.Orange,
        "severe" => Color.Red,
        "critical" => Color.Crimson,
        _ => Color.Green,
    };

    private static void InvalidateParents(Control control)
    {
        Control? current = control;
        while (current != null)
        {
            current.InvalidateMeasure();
            current.InvalidateArrange();
            current = current.Parent;
        }
    }
}

using System.Text;
using Content.Client._Onyx.HealthExaminable;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.Examine;

public sealed partial class ExamineSystem
{
    private const float PartStatusMaxWidth = 340f;
    private const float PartStatusTextMaxWidth = 310f;

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
            if (PartStatusTag.TryRead(node, out var summary, out var details))
                parent.AddChild(CreatePartStatus(summary, details));
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
            Margin = new Thickness(4, 4, 0, 4),
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

    private static Control CreatePartStatus(string summary, string details)
    {
        var hasDetails = !string.IsNullOrWhiteSpace(details);
        var arrow = new Label
        {
            Text = hasDetails ? "▸" : " ",
            MinWidth = 14,
            FontColorOverride = Color.Gray,
        };
        var summaryLabel = new RichTextLabel { MaxWidth = PartStatusTextMaxWidth };
        summaryLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(summary));

        var heading = new ContainerButton
        {
            MouseFilter = hasDetails ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore,
            MaxWidth = PartStatusMaxWidth,
            HorizontalAlignment = Control.HAlignment.Left,
        };
        var headingRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            MaxWidth = PartStatusMaxWidth,
        };
        headingRow.AddChild(arrow);
        headingRow.AddChild(summaryLabel);
        heading.AddChild(headingRow);

        var detailLabel = new RichTextLabel
        {
            Margin = new Thickness(18, 1, 0, 4),
            Visible = false,
            MaxWidth = PartStatusTextMaxWidth,
        };
        detailLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(details));

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MaxWidth = PartStatusMaxWidth,
            HorizontalAlignment = Control.HAlignment.Left,
            Margin = new Thickness(4, 0, 0, 2),
        };
        container.AddChild(heading);
        container.AddChild(detailLabel);

        if (hasDetails)
        {
            heading.OnPressed += _ =>
            {
                detailLabel.Visible = !detailLabel.Visible;
                arrow.Text = detailLabel.Visible ? "▾" : "▸";
                InvalidateParents(container);
            };
        }

        return container;
    }

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

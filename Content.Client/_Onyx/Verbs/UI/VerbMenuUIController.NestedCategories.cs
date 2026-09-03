using System.Linq;
using Content.Client.ContextMenu.UI;
using Content.Shared.Verbs;

namespace Content.Client.Verbs.UI;

public sealed partial class VerbMenuUIController
{
    private bool TryFillNestedVerbPopup(ContextMenuPopup popup)
    {
        if (!CurrentVerbs.Any(verb => verb.SubCategories is { Count: > 0 }))
            return false;

        var root = new NestedVerbCategoryNode(null);
        foreach (var verb in CurrentVerbs)
        {
            if (verb.Category == null)
            {
                root.Verbs.Add(verb);
                continue;
            }

            var node = root.GetOrAdd(verb.Category);
            if (verb.SubCategories != null)
            {
                foreach (var category in verb.SubCategories)
                    node = node.GetOrAdd(category);
            }
            node.Verbs.Add(verb);
        }

        foreach (var category in ExtraCategories)
            root.GetOrAdd(category);

        AddNestedContents(root, popup);
        popup.InvalidateMeasure();
        return true;
    }

    private void AddNestedContents(NestedVerbCategoryNode node, ContextMenuPopup popup)
    {
        var drawIcons = node.Verbs.Any(verb => verb.Icon != null || verb.IconEntity != null);
        foreach (var verb in node.Verbs)
        {
            var element = new VerbMenuElement(verb)
            {
                IconVisible = drawIcons,
                TextVisible = node.Category?.IconsOnly != true,
            };
            _context.AddElement(popup, element);
        }

        foreach (var child in node.Children)
            AddNestedCategory(child, popup);

        popup.MenuBody.Columns = node.Children.Count == 0 ? node.Category?.Columns ?? 1 : 1;
    }

    private void AddNestedCategory(NestedVerbCategoryNode node, ContextMenuPopup popup)
    {
        var style = FindTextStyle(node) ?? Verb.DefaultTextStyleClass;
        var element = new VerbMenuElement(node.Category!, style);
        var subMenu = new ContextMenuPopup(_context, element);
        element.SubMenu = subMenu;
        _context.AddElement(popup, element);
        AddNestedContents(node, subMenu);
    }

    private static string? FindTextStyle(NestedVerbCategoryNode node)
    {
        if (node.Verbs.FirstOrDefault() is { } verb)
            return verb.TextStyleClass;
        foreach (var child in node.Children)
        {
            if (FindTextStyle(child) is { } style)
                return style;
        }
        return null;
    }

    private sealed class NestedVerbCategoryNode(VerbCategory? category)
    {
        public readonly VerbCategory? Category = category;
        public readonly List<Verb> Verbs = new();
        public readonly List<NestedVerbCategoryNode> Children = new();

        public NestedVerbCategoryNode GetOrAdd(VerbCategory category)
        {
            foreach (var child in Children)
            {
                if (child.Category?.Text == category.Text)
                    return child;
            }

            var node = new NestedVerbCategoryNode(category);
            Children.Add(node);
            return node;
        }
    }
}

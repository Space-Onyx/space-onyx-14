using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Research.UI;

public sealed class ResearchItemRequirementControl : BoxContainer
{
    private readonly ResearchItemRequirement _requirement;
    private readonly IPrototypeManager _prototypes;
    private readonly SpriteSystem _sprites;
    private readonly TextureRect _icon;
    private readonly Label _name;
    private readonly Button _button;
    private readonly int _progress;
    private readonly Action<int>? _onSelectionChanged;
    private int _selected;

    public ResearchItemRequirementControl(
        ResearchItemRequirement requirement,
        int progress,
        int initialSelected,
        Action<int>? onSelectionChanged,
        IPrototypeManager prototypes,
        SpriteSystem sprites)
    {
        _requirement = requirement;
        _prototypes = prototypes;
        _sprites = sprites;
        _progress = progress;
        _onSelectionChanged = onSelectionChanged;
        var completed = progress >= Math.Max(1, requirement.Amount);
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;

        _selected = Math.Clamp(initialSelected, 0, requirement.AnyOf.Count - 1);

        AddChild(new PanelContainer
        {
            MinWidth = 5,
            MaxWidth = 5,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = completed ? Color.LimeGreen : Color.Crimson,
            },
        });

        _icon = new TextureRect { TextureScale = new(1, 1), Margin = new(1) };
        _name = new Label { VerticalAlignment = VAlignment.Center };
        _button = new Button
        {
            HorizontalExpand = true,
            Disabled = requirement.AnyOf.Count < 2,
            StyleClasses = { "ButtonSquare" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        _icon,
                        new Control { MinWidth = 5 },
                        _name,
                    },
                },
            },
        };
        AddChild(_button);

        _button.OnPressed += _ =>
        {
            _selected = (_selected + 1) % _requirement.AnyOf.Count;
            _onSelectionChanged?.Invoke(_selected);
            UpdateItem();
        };
        UpdateItem();
    }

    private void UpdateItem()
    {
        var id = _requirement.AnyOf[_selected];
        var prototype = _prototypes.Index(id);
        _icon.Texture = _sprites.GetPrototypeIcon(prototype).Default;
        _name.Text = _requirement.AnyOf.Count > 1
            ? Loc.GetString("research-console-item-requirement-alternative",
                ("item", prototype.Name),
                ("current", _selected + 1),
                ("total", _requirement.AnyOf.Count),
                ("progress", Math.Min(_progress, Math.Max(1, _requirement.Amount))),
                ("amount", Math.Max(1, _requirement.Amount)))
            : Loc.GetString("research-console-item-requirement-progress",
                ("item", prototype.Name),
                ("progress", Math.Min(_progress, Math.Max(1, _requirement.Amount))),
                ("amount", Math.Max(1, _requirement.Amount)));
    }
}

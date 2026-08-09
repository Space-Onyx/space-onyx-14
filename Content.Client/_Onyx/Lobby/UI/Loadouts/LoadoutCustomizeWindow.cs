using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Lobby.UI.Loadouts;

public sealed class LoadoutCustomizeWindow : DefaultWindow
{
    public event Action<string, string, Color?>? OnSubmitted;
    public event Action<Color>? OnColorPreview;
    public event Action? OnReverted;

    private bool _applied;
    private readonly LineEdit _nameEdit = new() { HorizontalExpand = true };
    private readonly TextEdit _descriptionEdit = new() { HorizontalExpand = true, VerticalExpand = true };
    private readonly ColorSelectorSliders? _colorSelector;

    public LoadoutCustomizeWindow(string title, string name, string description, Color? color)
    {
        Title = title;
        MinSize = new Vector2(360, color == null ? 320 : 560);
        _nameEdit.Text = name;
        _descriptionEdit.TextRope = new Rope.Leaf(description);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            Children =
            {
                new Label { Text = Loc.GetString("loadout-custom-name") },
                _nameEdit,
                new Label { Text = Loc.GetString("loadout-custom-description"), Margin = new Thickness(0, 8, 0, 0) },
                new PanelContainer { VerticalExpand = true, Children = { _descriptionEdit } },
            },
        };

        if (color != null)
        {
            _colorSelector = new ColorSelectorSliders { SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv, IsAlphaVisible = false, HorizontalExpand = true, Color = color.Value };
            _colorSelector.OnColorChanged += _ => OnColorPreview?.Invoke(_colorSelector.Color);
            body.AddChild(new Label { Text = Loc.GetString("loadout-custom-color"), Margin = new Thickness(0, 8, 0, 0) });
            body.AddChild(_colorSelector);
        }

        var apply = new Button { Text = Loc.GetString("loadout-custom-apply"), HorizontalAlignment = HAlignment.Right };
        apply.OnPressed += _ =>
        {
            _applied = true;
            OnSubmitted?.Invoke(_nameEdit.Text, Rope.Collapse(_descriptionEdit.TextRope), _colorSelector?.Color);
            Close();
        };
        body.AddChild(apply);
        OnClose += () => { if (!_applied) OnReverted?.Invoke(); };
        Contents.AddChild(body);
    }
}

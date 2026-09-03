using Content.Shared._Onyx.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class EmoteVisibilityPopup : Popup
{
    private readonly OptionButton _range;
    private readonly BoxContainer _radiusControls;
    private readonly Slider _radius;
    private readonly Label _radiusValue;
    private readonly OptionButton _perspective;
    private readonly CheckBox _showToGhosts;

    public EmoteVisibilityOptions Options => new(
        (EmoteVisibilityRange)_range.SelectedId,
        (int) _radius.Value,
        (EmotePerspective)_perspective.SelectedId,
        _showToGhosts.Pressed);

    public EmoteVisibilityPopup()
    {
        _range = new OptionButton();
        _range.AddItem(Loc.GetString("hud-chatbox-emote-visibility-range-radius"), (int) EmoteVisibilityRange.Radius);
        _range.AddItem(Loc.GetString("hud-chatbox-emote-visibility-range-surrounding"), (int) EmoteVisibilityRange.Surrounding);
        _range.SelectId((int) EmoteVisibilityOptions.Default.Range);

        _radiusValue = new Label();
        _radius = new Slider
        {
            MinValue = EmoteVisibilityOptions.MinRadius,
            MaxValue = EmoteVisibilityOptions.MaxRadius,
            Value = EmoteVisibilityOptions.Default.Radius,
            Rounded = true,
            HorizontalExpand = true,
            MinWidth = 170,
        };
        _radius.OnValueChanged += _ => UpdateRadiusValue();
        _radiusControls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Visible = EmoteVisibilityOptions.Default.Range == EmoteVisibilityRange.Radius,
            Children =
            {
                _radiusValue,
                _radius,
            },
        };
        _range.OnItemSelected += args =>
        {
            _range.SelectId(args.Id);
            _radiusControls.Visible = args.Id == (int) EmoteVisibilityRange.Radius;
        };
        UpdateRadiusValue();

        _perspective = new OptionButton();
        _perspective.AddItem(Loc.GetString("hud-chatbox-emote-visibility-perspective-first-person"), (int) EmotePerspective.FirstPerson);
        _perspective.AddItem(Loc.GetString("hud-chatbox-emote-visibility-perspective-third-person"), (int) EmotePerspective.ThirdPerson);
        _perspective.SelectId((int) EmoteVisibilityOptions.Default.Perspective);
        _perspective.OnItemSelected += args => _perspective.SelectId(args.Id);

        _showToGhosts = new CheckBox
        {
            Text = Loc.GetString("hud-chatbox-emote-visibility-show-to-ghosts"),
            Pressed = EmoteVisibilityOptions.Default.ShowToGhosts,
        };

        AddChild(new PanelContainer
        {
            StyleClasses = { "BorderedWindowPanel" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 6,
                    Margin = new Thickness(10),
                    MinWidth = 260,
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("hud-chatbox-emote-visibility-general"),
                            StyleClasses = { "LabelHeading" },
                        },
                        CreateSection("hud-chatbox-emote-visibility-range", _range, _radiusControls),
                        CreateSection("hud-chatbox-emote-visibility-perspective", _perspective),
                        CreateSection("hud-chatbox-emote-visibility-observers", _showToGhosts),
                    },
                },
            },
        });
    }

    private void UpdateRadiusValue()
    {
        _radiusValue.Text = Loc.GetString("hud-chatbox-emote-visibility-radius-value", ("radius", (int) _radius.Value));
    }

    private static PanelContainer CreateSection(string title, params Control[] controls)
    {
        var contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(title),
                    StyleClasses = { "LabelSubText" },
                },
            },
        };

        foreach (var control in controls)
            contents.AddChild(control);

        return new PanelContainer
        {
            StyleClasses = { "BackgroundPanel" },
            Children = { contents },
        };
    }
}

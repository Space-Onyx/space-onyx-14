using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatSearchPopup : Popup
{
    private readonly LineEdit _searchInput;

    public event Action<string>? OnSearchChanged;

    public ChatSearchPopup()
    {
        _searchInput = new LineEdit
        {
            PlaceHolder = Loc.GetString("hud-chatbox-search-placeholder"),
            HorizontalExpand = true,
            MinWidth = 210,
        };
        _searchInput.OnTextChanged += _ => OnSearchChanged?.Invoke(_searchInput.Text);

        var clearButton = new Button
        {
            Text = "X",
            ToolTip = Loc.GetString("hud-chatbox-search-clear-tooltip"),
            StyleClasses = { "ButtonSquare" },
        };
        clearButton.OnPressed += _ => ClearSearch();

        AddChild(new PanelContainer
        {
            StyleClasses = { "BorderedWindowPanel" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 2,
                    Margin = new Thickness(4),
                    Children =
                    {
                        _searchInput,
                        clearButton,
                    },
                },
            },
        });
    }

    public void ClearSearch()
    {
        _searchInput.Text = string.Empty;
        _searchInput.GrabKeyboardFocus();
    }

    public void FocusSearch() => _searchInput.GrabKeyboardFocus();
}

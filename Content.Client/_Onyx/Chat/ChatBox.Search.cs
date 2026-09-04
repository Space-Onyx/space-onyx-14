using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private string? _searchText;
    private ChatSearchButton? _searchButton;

    private void InitializeChatSearch()
    {
        _searchButton = new ChatSearchButton
        {
            Name = "SearchButton",
            StyleClasses = { ChatInputBox.StyleClassChatFilterOptionButton },
            HorizontalAlignment = HAlignment.Right,
        };
        _searchButton.Popup.OnSearchChanged += OnSearchChanged;
        _searchButton.OnToggled += OnSearchToggled;

        var searchBar = new BoxContainer
        {
            Name = "SearchBar",
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            HorizontalExpand = true,
            Margin = new Thickness(8, 0, 8, 0),
            Children =
            {
                new Control { HorizontalExpand = true },
                _searchButton,
            },
        };

        var messagesBox = (BoxContainer) Contents.Parent!;
        messagesBox.SeparationOverride = 1;
        Contents.Margin = new Thickness(8, 2, 8, 4);
        messagesBox.AddChild(searchBar);
        searchBar.SetPositionInParent(0);
    }

    private void ShutdownChatSearch()
    {
        if (_searchButton == null)
            return;

        _searchButton.Popup.OnSearchChanged -= OnSearchChanged;
        _searchButton.OnToggled -= OnSearchToggled;
        _searchButton.Popup.Close();
        _searchButton = null;
    }

    private void OnSearchChanged(string text)
    {
        _searchText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Repopulate();
    }

    private void OnSearchToggled(BaseButton.ButtonToggledEventArgs args)
    {
        if (args.Pressed)
            _searchButton?.Popup.FocusSearch();
    }

    private bool MatchesSearch(ChatMessage msg)
    {
        if (string.IsNullOrEmpty(_searchText))
            return true;

        return msg.Message.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || msg.WrappedMessage.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }
}

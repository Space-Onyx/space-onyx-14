using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private string? _onyxSearchText;
    private ChatSearchButton? _onyxSearchButton;

    private void InitializeOnyxChatSearch()
    {
        _onyxSearchButton = new ChatSearchButton
        {
            Name = "SearchButton",
            StyleClasses = { ChatInputBox.StyleClassChatFilterOptionButton },
            HorizontalAlignment = HAlignment.Right,
        };
        _onyxSearchButton.Popup.OnSearchChanged += OnOnyxSearchChanged;
        _onyxSearchButton.OnToggled += OnOnyxSearchToggled;

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
                _onyxSearchButton,
            },
        };

        var messagesBox = (BoxContainer) Contents.Parent!;
        messagesBox.SeparationOverride = 1;
        Contents.Margin = new Thickness(8, 2, 8, 4);
        messagesBox.AddChild(searchBar);
        searchBar.SetPositionInParent(0);
    }

    private void ShutdownOnyxChatSearch()
    {
        if (_onyxSearchButton == null)
            return;

        _onyxSearchButton.Popup.OnSearchChanged -= OnOnyxSearchChanged;
        _onyxSearchButton.OnToggled -= OnOnyxSearchToggled;
        _onyxSearchButton.Popup.Close();
        _onyxSearchButton = null;
    }

    private void OnOnyxSearchChanged(string text)
    {
        _onyxSearchText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Repopulate();
    }

    private void OnOnyxSearchToggled(BaseButton.ButtonToggledEventArgs args)
    {
        if (args.Pressed)
            _onyxSearchButton?.Popup.FocusSearch();
    }

    private bool MatchesOnyxSearch(ChatMessage msg)
    {
        if (string.IsNullOrEmpty(_onyxSearchText))
            return true;

        return msg.Message.Contains(_onyxSearchText, StringComparison.OrdinalIgnoreCase)
            || msg.WrappedMessage.Contains(_onyxSearchText, StringComparison.OrdinalIgnoreCase);
    }
}

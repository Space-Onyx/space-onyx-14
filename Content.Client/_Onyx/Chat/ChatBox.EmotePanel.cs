using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private static readonly Color EmotePanelActiveModulate = new(1.35f, 1.35f, 1.35f);

    private EmotePanelSection? _emoteSection;
    private EmotePanelButton? _emoteButton;

    private void InitializeEmotePanel()
    {
        if (Contents.Parent is not BoxContainer messagesBox)
            return;

        BoxContainer? searchBar = null;
        foreach (var child in messagesBox.Children)
        {
            if (child.Name == "SearchBar" && child is BoxContainer bar)
            {
                searchBar = bar;
                break;
            }
        }

        if (searchBar == null)
            return;

        _emoteButton = new EmotePanelButton
        {
            Name = "EmotePanelButton",
            StyleClasses = { ChatInputBox.StyleClassChatFilterOptionButton },
        };
        _emoteButton.OnToggled += OnEmotePanelToggled;
        searchBar.AddChild(_emoteButton);

        _emoteSection = new EmotePanelSection
        {
            Name = "EmotePanel",
            HorizontalExpand = true,
            Visible = false,
        };
        messagesBox.AddChild(_emoteSection);
        _emoteSection.SetPositionInParent(1);
    }

    private void OnEmotePanelToggled(BaseButton.ButtonToggledEventArgs args)
    {
        if (_emoteSection == null || _emoteButton == null)
            return;

        _emoteSection.EnsureInitialized();
        _emoteSection.Visible = args.Pressed;
        _emoteButton.ModulateSelfOverride = args.Pressed ? EmotePanelActiveModulate : null;
    }
}

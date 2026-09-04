using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private static readonly Color OnyxEmotePanelActiveModulate = new(1.35f, 1.35f, 1.35f);

    private EmotePanelSection? _onyxEmoteSection;
    private EmotePanelButton? _onyxEmoteButton;

    private void InitializeOnyxEmotePanel()
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

        _onyxEmoteButton = new EmotePanelButton
        {
            Name = "EmotePanelButton",
            StyleClasses = { ChatInputBox.StyleClassChatFilterOptionButton },
        };
        _onyxEmoteButton.OnToggled += OnOnyxEmotePanelToggled;
        searchBar.AddChild(_onyxEmoteButton);

        _onyxEmoteSection = new EmotePanelSection
        {
            Name = "EmotePanel",
            HorizontalExpand = true,
            Visible = false,
        };
        messagesBox.AddChild(_onyxEmoteSection);
        _onyxEmoteSection.SetPositionInParent(1);
    }

    private void OnOnyxEmotePanelToggled(BaseButton.ButtonToggledEventArgs args)
    {
        if (_onyxEmoteSection == null || _onyxEmoteButton == null)
            return;

        _onyxEmoteSection.EnsureInitialized();
        _onyxEmoteSection.Visible = args.Pressed;
        _onyxEmoteButton.ModulateSelfOverride = args.Pressed ? OnyxEmotePanelActiveModulate : null;
    }
}

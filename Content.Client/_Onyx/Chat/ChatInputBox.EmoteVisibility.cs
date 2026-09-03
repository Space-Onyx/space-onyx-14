namespace Content.Client.UserInterface.Systems.Chat.Controls;

public partial class ChatInputBox
{
    public EmoteVisibilityButton EmoteVisibilityButton { get; private set; } = default!;

    private void InitializeEmoteVisibility()
    {
        EmoteVisibilityButton = new EmoteVisibilityButton
        {
            Name = "EmoteVisibilityButton",
            Visible = false,
            StyleClasses = { StyleClassChatFilterOptionButton },
        };
        Container.AddChild(EmoteVisibilityButton);
    }

    public void UpdateEmoteVisibility(bool visible)
    {
        EmoteVisibilityButton.Visible = visible;
        if (!visible)
            EmoteVisibilityButton.Popup.Close();
    }
}

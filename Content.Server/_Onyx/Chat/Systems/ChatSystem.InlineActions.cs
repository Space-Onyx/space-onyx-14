using Content.Server._Onyx.Chat;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private void TryTriggerInlineActionEmotes(EntityUid source, List<string>? actions, bool forced, bool ignoreActionBlocker)
    {
        if (actions == null)
            return;

        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        foreach (var action in actions)
        {
            TryEmoteChatInput(source, action);
        }
    }
}
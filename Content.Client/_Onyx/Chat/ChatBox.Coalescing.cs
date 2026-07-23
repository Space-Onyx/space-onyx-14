using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private IConfigurationManager _onyxConfig = default!;
    private bool _onyxCoalesceMessages;
    private (string Message, Color Color)? _onyxLastLine;
    private int _onyxRepeatCount;

    private void InitializeOnyxChatCoalescing()
    {
        _onyxConfig = IoCManager.Resolve<IConfigurationManager>();
        _onyxCoalesceMessages = _onyxConfig.GetCVar(CCVars.ChatCoalesceIdenticalMessages);
        _onyxConfig.OnValueChanged(CCVars.ChatCoalesceIdenticalMessages, OnCoalescingChanged);
    }

    private void ShutdownOnyxChatCoalescing()
    {
        _onyxConfig.UnsubValueChanged(CCVars.ChatCoalesceIdenticalMessages, OnCoalescingChanged);
    }

    private void OnCoalescingChanged(bool enabled)
    {
        _onyxCoalesceMessages = enabled;
        Repopulate();
    }

    private bool TryAddCoalescedMessage(ChatMessage message, Color color)
    {
        var line = (message.WrappedMessage, color);
        if (_onyxCoalesceMessages && message.CanCoalesce && _onyxLastLine == line && Contents.EntryCount > 0)
        {
            _onyxRepeatCount++;
            AddLine(message.WrappedMessage, color, _onyxRepeatCount + 1);
            Contents.RemoveEntry(^2);
            return true;
        }

        _onyxLastLine = line;
        _onyxRepeatCount = 0;
        return false;
    }

    private void ResetOnyxChatCoalescing()
    {
        _onyxLastLine = null;
        _onyxRepeatCount = 0;
    }

    private void AddLine(string message, Color color, int repeatCount)
    {
        var formatted = new FormattedMessage(4);
        formatted.PushColor(color);
        formatted.AddMarkupOrThrow(message);
        formatted.Pop();
        formatted.AddMarkupOrThrow(Loc.GetString("chat-system-repeated-message-counter",
            ("count", repeatCount),
            ("size", 8 + Math.Min(repeatCount / 6, 5))));
        Contents.AddMessage(formatted, tagsAllowed: null);
    }
}

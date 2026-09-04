using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private IConfigurationManager _coalesceConfig = default!;
    private bool _coalesceMessages;
    private (string Message, Color Color)? _lastCoalescedLine;
    private int _coalescedRepeatCount;

    private void InitializeChatCoalescing()
    {
        _coalesceConfig = IoCManager.Resolve<IConfigurationManager>();
        _coalesceMessages = _coalesceConfig.GetCVar(CCVars.ChatCoalesceIdenticalMessages);
        _coalesceConfig.OnValueChanged(CCVars.ChatCoalesceIdenticalMessages, OnCoalescingChanged);
    }

    private void ShutdownChatCoalescing()
    {
        _coalesceConfig.UnsubValueChanged(CCVars.ChatCoalesceIdenticalMessages, OnCoalescingChanged);
    }

    private void OnCoalescingChanged(bool enabled)
    {
        _coalesceMessages = enabled;
        Repopulate();
    }

    private bool TryAddCoalescedMessage(ChatMessage message, Color color)
    {
        var line = (message.WrappedMessage, color);
        if (_coalesceMessages && message.CanCoalesce && _lastCoalescedLine == line && Contents.EntryCount > 0)
        {
            _coalescedRepeatCount++;
            AddLine(message.WrappedMessage, color, _coalescedRepeatCount + 1);
            Contents.RemoveEntry(^2);
            return true;
        }

        _lastCoalescedLine = line;
        _coalescedRepeatCount = 0;
        return false;
    }

    private void ResetChatCoalescing()
    {
        _lastCoalescedLine = null;
        _coalescedRepeatCount = 0;
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

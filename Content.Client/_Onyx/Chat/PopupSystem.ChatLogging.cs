using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.Popups;

public sealed partial class PopupSystem
{
    private static readonly Dictionary<PopupType, int> OnyxPopupFontSizes = new()
    {
        { PopupType.Medium, 12 },
        { PopupType.MediumCaution, 12 },
        { PopupType.Large, 15 },
        { PopupType.LargeCaution, 15 },
    };

    private bool _onyxLogActionsInChat;

    private void InitializeOnyxChatLogging()
    {
        _configManager.OnValueChanged(CCVars.ChatLogActions, OnChatLoggingChanged, true);
    }

    private void ShutdownOnyxChatLogging()
    {
        _configManager.UnsubValueChanged(CCVars.ChatLogActions, OnChatLoggingChanged);
    }

    private void OnChatLoggingChanged(bool enabled)
    {
        _onyxLogActionsInChat = enabled;
    }

    private void LogPopupInChat(string message, PopupType type, EntityCoordinates coordinates)
    {
        if (!_onyxLogActionsInChat ||
            _playerManager.LocalEntity is not { } player ||
            !_examine.InRangeUnOccluded(player, coordinates, 10))
            return;

        var size = OnyxPopupFontSizes.GetValueOrDefault(type, 10);
        var color = type is PopupType.SmallCaution or PopupType.MediumCaution or PopupType.LargeCaution
            ? "#C62828"
            : "#AEABC4";
        var escaped = FormattedMessage.EscapeText(message);
        var wrapped = $"[font size={size}][color={color}]{escaped}[/color][/font]";
        var chatMessage = new ChatMessage(ChatChannel.Emotes,
            message,
            wrapped,
            NetEntity.Invalid,
            null,
            canCoalesce: true);

        _uiManager.GetUIController<ChatUIController>().ProcessChatMessage(chatMessage, speechBubble: false);
    }
}

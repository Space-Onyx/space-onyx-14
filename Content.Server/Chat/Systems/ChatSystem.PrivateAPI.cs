using System.Linq;
using Content.Server._Onyx.Chat;
// <Onyx-Languages>
using Content.Shared._Onyx.Language;
// </Onyx-Languages>
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
// <Onyx-SignLanguage>
using Content.Shared.Eye.Blinding.Components;
// </Onyx-SignLanguage>
using Content.Shared.Radio;
using Content.Shared.Speech; // <Onyx-LanguageAppearance>
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes; // <Onyx-OSayLanguage>
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private void SendEntitySpeak(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        ProtoId<LanguagePrototype>? languageOverride = null // <Onyx-OSayLanguage>
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, originalMessage);

        if (message.Length == 0)
            return;

        // <Onyx-InlineActions>
        var protectedMessage = InlineActionFormatter.ProtectActions(message, out var inlineReplacements, out var inlineActions);
        TryTriggerInlineActionEmotes(source, inlineActions, false, ignoreActionBlocker);
        var restoredMessage = InlineActionFormatter.RestoreActions(protectedMessage, inlineReplacements);
        // </Onyx-InlineActions>

        var speech = GetSpeechVerb(source, restoredMessage);

        // get the entity's apparent name (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
            // Check for a speech verb override
            if (nameEv.SpeechVerb != null && ProtoMan.Resolve(nameEv.SpeechVerb, out var proto))
                speech = proto;
        }

        name = FormattedMessage.EscapeText(name);

        var content = FormattedMessage.EscapeText(restoredMessage);
        var inlineFormattedMessage = InlineActionFormatter.Format(content); // <Onyx-InlineActions>
        // <Onyx-Languages>
        var speechVerb = Loc.GetString(_random.Pick(speech.SpeechVerbStrings));
        // </Onyx-Languages>
        // <Onyx-Loudspeaker>
        var loudspeakerFontSize = GetLoudspeakerFontSize(source, false);
        // </Onyx-Loudspeaker>
        // <Onyx-Languages-edited>
        var language = languageOverride is { } languageId && ProtoMan.TryIndex(languageId, out LanguagePrototype? overrideLanguage)
            ? overrideLanguage
            : _language.GetCurrentLanguage(source); // <Onyx-OSayLanguage-edited>
        var wrappedMessage = WrapLanguageMessage(
            speech.Bold ? "chat-manager-entity-say-language-bold-wrap-message" : "chat-manager-entity-say-language-wrap-message",
            name,
            speechVerb,
            inlineFormattedMessage,
            speech,
            language,
            loudspeakerFontSize);
        // <Onyx-SignLanguage-edited>
        var isSignLanguage = language.RequiresSight;
        // </Onyx-SignLanguage-edited>
        var obfuscated = InlineActionFormatter.RestoreActions(_language.Obfuscate(protectedMessage, language), inlineReplacements);
        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            if (session.AttachedEntity is not { } listener)
                continue;

            // <Onyx-SignLanguage-edited>
            if (isSignLanguage
                    ? TryComp<BlindableComponent>(listener, out var blind) && blind.IsBlind ||
                      !_examineSystem.InRangeUnOccluded(source, listener, VoiceRange)
                    : !CanHear(listener))
                continue;
            // </Onyx-SignLanguage-edited>

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            var understood = _language.CanUnderstand(listener, language.ID);
            var perceived = understood ? restoredMessage : obfuscated;
            var perceivedContent = InlineActionFormatter.Format(FormattedMessage.EscapeText(perceived));
            var perceivedWrap = WrapLanguageMessage(
                speech.Bold ? "chat-manager-entity-say-language-bold-wrap-message" : "chat-manager-entity-say-language-wrap-message",
                name,
                speechVerb,
                perceivedContent,
                speech,
                language,
                loudspeakerFontSize);
            _chatManager.ChatMessageToOne(ChatChannel.Local, perceived, perceivedWrap, source,
                entRange == MessageRangeCheckResult.HideChat, session.Channel);
        }
        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Local, message, wrappedMessage, GetNetEntity(source), null, MessageRangeHideChatForReplay(range)));
        // </Onyx-Languages-edited>

        var ev = new EntitySpokeEvent(source, message, originalMessage, null, null);
        RaiseLocalEvent(source, ev, true);

        // To avoid logging any messages sent by entities that are not players, like vendors, cloning, etc.
        // Also doesn't log if hideLog is true.
        if (!HasComp<ActorComponent>(source) || hideLog)
            return;

        if (originalMessage == message)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source} as {name}: {originalMessage}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source}: {originalMessage}.");
        }
        else
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source} as {name}, original: {originalMessage}, transformed: {message}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source}, original: {originalMessage}, transformed: {message}.");
        }
    }

    private void SendEntityWhisper(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        RadioChannelPrototype? channel,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        ProtoId<LanguagePrototype>? languageOverride = null // <Onyx-OSayLanguage>
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, FormattedMessage.RemoveMarkupOrThrow(originalMessage));
        if (message.Length == 0)
            return;

        // <Onyx-InlineActions>
        var protectedMessage = InlineActionFormatter.ProtectActions(message, out var inlineReplacements, out var inlineActions);
        TryTriggerInlineActionEmotes(source, inlineActions, false, ignoreActionBlocker);
        var restoredMessage = InlineActionFormatter.RestoreActions(protectedMessage, inlineReplacements);
        // </Onyx-InlineActions>

        // <Onyx-Languages-edited>
        var language = languageOverride is { } languageId && ProtoMan.TryIndex(languageId, out LanguagePrototype? overrideLanguage)
            ? overrideLanguage
            : _language.GetCurrentLanguage(source); // <Onyx-OSayLanguage-edited>
        // <Onyx-SignLanguage-edited>
        var isSignLanguage = language.RequiresSight;
        // </Onyx-SignLanguage-edited>
        var languageObfuscatedMessage = InlineActionFormatter.RestoreActions(_language.Obfuscate(protectedMessage, language), inlineReplacements);
        var obfuscatedMessage = ObfuscateMessageReadability(restoredMessage, 0.2f);
        // </Onyx-Languages-edited>

        // get the entity's name by visual identity (if no override provided).
        string nameIdentity = FormattedMessage.EscapeText(nameOverride ?? Identity.Name(source, EntityManager));
        // get the entity's name by voice (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
        }
        name = FormattedMessage.EscapeText(name);

        var content = FormattedMessage.EscapeText(restoredMessage);
        var inlineFormattedMessage = InlineActionFormatter.Format(content); // <Onyx-InlineActions>
        var wrappedMessage = WrapLanguageWhisper(name, inlineFormattedMessage, language);

        foreach (var (session, data) in GetRecipients(source, WhisperMuffledRange))
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            // <Onyx-OrganHearing>
            // <Onyx-SignLanguage-edited>
            if (isSignLanguage
                    ? TryComp<BlindableComponent>(listener, out var blind) && blind.IsBlind ||
                      !_examineSystem.InRangeUnOccluded(source, listener, WhisperMuffledRange)
                    : !CanHear(listener))
                continue;
            // </Onyx-SignLanguage-edited>
            // </Onyx-OrganHearing>

            if (MessageRangeCheck(session, data, range) != MessageRangeCheckResult.Full)
                continue; // Won't get logged to chat, and ghosts are too far away to see the pop-up, so we just won't send it to them.

            // <Onyx-Languages-edited>
            var understoodMessage = _language.CanUnderstand(listener, language.ID) ? restoredMessage : languageObfuscatedMessage;
            var understoodWrap = WrapLanguageWhisper(
                name,
                InlineActionFormatter.Format(FormattedMessage.EscapeText(understoodMessage)),
                language);

            if (data.Range <= WhisperClearRange || data.Observer)
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, understoodMessage, understoodWrap, source, false, session.Channel);
            //If listener is too far, they only hear fragments of the message
            else if (_examineSystem.InRangeUnOccluded(source, listener, WhisperMuffledRange))
            {
                var muffled = ObfuscateMessageReadability(understoodMessage, 0.2f);
                var muffledWrap = WrapLanguageWhisper(
                    nameIdentity,
                    InlineActionFormatter.Format(FormattedMessage.EscapeText(muffled)),
                    language);
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, muffled, muffledWrap, source, false, session.Channel);
            }
            //If listener is too far and has no line of sight, they can't identify the whisperer's identity
            else
            {
                var muffled = ObfuscateMessageReadability(understoodMessage, 0.2f);
                var unknownWrap = WrapLanguageWhisper(
                    string.Empty,
                    InlineActionFormatter.Format(FormattedMessage.EscapeText(muffled)),
                    language,
                    "chat-manager-entity-whisper-unknown-language-wrap-message");
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, muffled, unknownWrap, source, false, session.Channel);
            }
            // </Onyx-Languages-edited>
        }

        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Whisper, message, wrappedMessage, GetNetEntity(source), null, MessageRangeHideChatForReplay(range)));

        var ev = new EntitySpokeEvent(source, message, originalMessage, channel, obfuscatedMessage);
        RaiseLocalEvent(source, ev, true);
        if (!hideLog)
            if (originalMessage == message)
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source} as {name}: {originalMessage}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source}: {originalMessage}.");
            }
            else
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source} as {name}, original: {originalMessage}, transformed: {message}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source}, original: {originalMessage}, transformed: {message}.");
            }
    }

    // <Onyx-LanguageAppearance>
    private string WrapLanguageMessage(
        LocId wrapId,
        string name,
        string verb,
        string message,
        SpeechVerbPrototype speech,
        LanguagePrototype language,
        int? fontSize = null)
    {
        var color = language.Speech.Color is { } overrideColor
            ? Color.InterpolateBetween(Color.White, overrideColor, overrideColor.A)
            : Color.White;

        return Loc.GetString(wrapId,
            ("entityName", name),
            ("verb", verb),
            ("color", color),
            ("fontType", language.Speech.FontId ?? speech.FontId),
            ("boldFontType", language.Speech.BoldFontId ?? language.Speech.FontId ?? speech.FontId),
            ("fontSize", fontSize ?? language.Speech.FontSize ?? speech.FontSize),
            ("message", message));
    }

    private string WrapLanguageWhisper(
        string name,
        string message,
        LanguagePrototype language,
        string wrapId = "chat-manager-entity-whisper-language-wrap-message")
    {
        var color = language.Speech.Color is { } overrideColor
            ? Color.InterpolateBetween(Color.White, overrideColor, overrideColor.A)
            : Color.White;

        return Loc.GetString(wrapId,
            ("entityName", name),
            ("color", color),
            ("fontType", language.Speech.FontId ?? "DefaultItalic"),
            ("fontSize", language.Speech.FontSize ?? 11),
            ("message", message));
    }
    // </Onyx-LanguageAppearance>

    protected override void SendEntityEmote(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool checkEmote = true,
        bool ignoreActionBlocker = false,
        NetUserId? author = null
        )
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided).
        var ent = Identity.Entity(source, EntityManager);
        string name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        var wrappedMessage = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", name),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkupOrThrow(action)));

        if (checkEmote &&
            !TryEmoteChatInput(source, action))
            return;

        SendInVoiceRange(ChatChannel.Emotes, action, wrappedMessage, source, range, author, requiresHearing: false); // <Onyx-OrganHearing-edited>
        if (!hideLog)
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source}: {action}");
    }

    // ReSharper disable once InconsistentNaming
    private void SendLOOC(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        var name = FormattedMessage.EscapeText(Identity.Name(source, EntityManager));

        if (_adminManager.IsAdmin(player))
        {
            if (!_adminLoocEnabled) return;
        }
        else if (!_loocEnabled) return;

        // If crit player LOOC is disabled, don't send the message at all.
        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        var wrappedMessage = Loc.GetString("chat-manager-entity-looc-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(message)));

        SendInVoiceRange(ChatChannel.LOOC, message, wrappedMessage, source, hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal, player.UserId);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"LOOC from {source}: {message}");
    }

    private void SendDeadChat(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        if (!_adminManager.IsAdmin(player) && !_deadChatEnabled)
            return;

        var clients = GetDeadChatClients();
        var playerName = Name(source);
        string wrappedMessage;
        if (_adminManager.IsAdmin(player))
        {
            wrappedMessage = Loc.GetString("chat-manager-send-admin-dead-chat-wrap-message",
                ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
                ("userName", player.Channel.UserName),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Admin dead chat from {source}: {message}");
        }
        else
        {
            wrappedMessage = Loc.GetString("chat-manager-send-dead-chat-wrap-message",
                ("deadChannelName", Loc.GetString("chat-manager-dead-channel-name")),
                ("playerName", (playerName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {source}: {message}");
        }

        _chatManager.ChatMessageToMany(ChatChannel.Dead, message, wrappedMessage, source, hideChat, true, clients.ToList(), author: player.UserId);
    }
}

using System.Globalization;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
// <Onyx-Languages>
using Content.Server._Onyx.Language;
// </Onyx-Languages>
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Onyx.Loudspeaker.Events; // <Onyx-Loudspeaker>
using Content.Shared._Onyx.Chat; // <Onyx-EmoteVisibility>
using Content.Server._Onyx.Chat; // <Onyx-CollectiveMind>
using Content.Shared.Players.RateLimiting;
using Content.Shared.Speech.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility; // <Onyx-CollectiveMind>

namespace Content.Server.Chat.Systems;

// TODO refactor whatever active warzone this class and chatmanager have become
/// <summary>
///     ChatSystem is responsible for in-simulation chat handling, such as whispering, speaking, emoting, etc.
///     ChatSystem depends on ChatManager to actually send the messages.
/// </summary>
public sealed partial class ChatSystem : SharedChatSystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IChatSanitizationManager _sanitizer = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ReplacementAccentSystem _wordreplacement = default!;
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private EntityQuery<GhostHearingComponent> _ghostHearingQuery = default!;
    // <Onyx-Languages>
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private CollectiveMindSystem _collectiveMind = default!; // <Onyx-CollectiveMind>
    // </Onyx-Languages>

    // <Onyx-Loudspeaker>
    public int? GetLoudspeakerFontSize(EntityUid source, bool radio)
    {
        var getLoudspeakers = new GetLoudspeakerEvent();
        RaiseLocalEvent(source, ref getLoudspeakers);
        if (getLoudspeakers.Loudspeakers == null)
            return null;

        foreach (var loudspeaker in getLoudspeakers.Loudspeakers)
        {
            var data = new GetLoudspeakerDataEvent();
            RaiseLocalEvent(loudspeaker, ref data);
            if (data.IsActive && (radio ? data.AffectRadio : data.AffectChat))
                return data.FontSize;
        }

        return null;
    }
    // </Onyx-Loudspeaker>

    // Corvax-TTS-Start: Moved from Server to Shared
    // public const int VoiceRange = 10; // how far voice goes in world units
    // public const int WhisperClearRange = 2; // how far whisper goes while still being understandable, in world units
    // public const int WhisperMuffledRange = 5; // how far whisper goes at all, in world units
    // Corvax-TTS-End

    private bool _loocEnabled = true;
    private bool _deadLoocEnabled;
    private bool _critLoocEnabled;
    private readonly bool _adminLoocEnabled = true;

    private bool _deadChatEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configurationManager, CCVars.LoocEnabled, OnLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.DeadLoocEnabled, OnDeadLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.CritLoocEnabled, OnCritLoocEnabledChanged, true);
        Subs.CVar(_configurationManager, CCVars.DeadChatEnabled, OnDeadChatEnabledChanged, true);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameChange);
    }

    private void OnLoocEnabledChanged(bool val)
    {
        if (_loocEnabled == val) return;

        _loocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-looc-chat-enabled-message" : "chat-manager-looc-chat-disabled-message"));
    }

    private void OnDeadLoocEnabledChanged(bool val)
    {
        if (_deadLoocEnabled == val) return;

        _deadLoocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-dead-looc-chat-enabled-message" : "chat-manager-dead-looc-chat-disabled-message"));
    }

    private void OnCritLoocEnabledChanged(bool val)
    {
        if (_critLoocEnabled == val)
            return;

        _critLoocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-crit-looc-chat-enabled-message" : "chat-manager-crit-looc-chat-disabled-message"));
    }

    private void OnDeadChatEnabledChanged(bool val)
    {
        if (_deadChatEnabled == val)
            return;

        _deadChatEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-dead-chat-enabled-message" : "chat-manager-dead-chat-disabled-message"));
    }

    private void OnGameChange(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.InRound:
                if (!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, false);
                break;
            case GameRunLevel.PostRound:
            case GameRunLevel.PreRoundLobby:
                if (!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, true);
                break;
        }
    }

    /// <inheritdoc />
    public override void TrySendInGameICMessage(
        EntityUid source,
        string message,
        InGameICChatType desiredType,
        bool hideChat,
        bool hideLog = false,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false,
        Robust.Shared.Prototypes.ProtoId<Content.Shared._Onyx.Language.LanguagePrototype>? languageOverride = null, // <Onyx-OSayLanguage> <Onyx-EmoteVisibility-edited>
        EmoteVisibilityOptions? emoteVisibility = null) // <Onyx-OSayLanguage> <Onyx-EmoteVisibility>
    {
        TrySendInGameICMessage(source, message, desiredType, hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal, hideLog, shell, player, nameOverride, checkRadioPrefix, ignoreActionBlocker, languageOverride, emoteVisibility); // <Onyx-OSayLanguage-edited> <Onyx-EmoteVisibility-edited>
    }

    /// <inheritdoc />
    public override void TrySendInGameICMessage(
        EntityUid source,
        string message,
        InGameICChatType desiredType,
        ChatTransmitRange range,
        bool hideLog = false,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        string? nameOverride = null,
        bool checkRadioPrefix = true,
        bool ignoreActionBlocker = false,
        Robust.Shared.Prototypes.ProtoId<Content.Shared._Onyx.Language.LanguagePrototype>? languageOverride = null, // <Onyx-OSayLanguage> <Onyx-EmoteVisibility-edited>
        EmoteVisibilityOptions? emoteVisibility = null // <Onyx-EmoteVisibility>
        )
    {
        if (HasComp<GhostComponent>(source))
        {
            // Ghosts can only send dead chat messages, so we'll forward it to InGame OOC.
            TrySendInGameOOCMessage(source, message, InGameOOCChatType.Dead, range == ChatTransmitRange.HideChat, shell, player);
            return;
        }

        if (player != null && _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)
            return;

        // Sus
        if (player?.AttachedEntity is { Valid: true } entity && source != entity)
        {
            return;
        }

        if (!CanSendInGame(message, shell, player))
            return;

        ignoreActionBlocker = CheckIgnoreSpeechBlocker(source, ignoreActionBlocker);

        // this method is a disaster
        // every second i have to spend working with this code is fucking agony
        // scientists have to wonder how any of this was merged
        // coding any game admin feature that involves chat code is pure torture
        // changing even 10 lines of code feels like waterboarding myself
        // and i dont feel like vibe checking 50 code paths
        // so we set this here
        // todo free me from chat code
        if (player != null)
        {
            _chatManager.EnsurePlayer(player.UserId).AddEntity(GetNetEntity(source));
        }

        if (desiredType == InGameICChatType.Speak && message.StartsWith(LocalPrefix))
        {
            // prevent radios and remove prefix.
            checkRadioPrefix = false;
            message = message[1..];
        }

        bool shouldCapitalize = (desiredType != InGameICChatType.Emote);
        bool shouldPunctuate = _configurationManager.GetCVar(CCVars.ChatPunctuation);
        // Capitalizing the word I only happens in English, so we check language here
        bool shouldCapitalizeTheWordI = (!CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en")
            || (CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en");

        // <Onyx-CollectiveMind-edited>
        var collectivePrefix = string.Empty;
        if (desiredType == InGameICChatType.CollectiveMind && message.StartsWith(CollectiveMindPrefix))
        {
            var prefixLength = Math.Min(message.Length, 2);
            collectivePrefix = message[..prefixLength];
            message = message[prefixLength..];
        }

        message = collectivePrefix + SanitizeInGameICMessage(source, message, out var emoteStr, shouldCapitalize, shouldPunctuate, shouldCapitalizeTheWordI);
        // </Onyx-CollectiveMind-edited>

        // Was there an emote in the message? If so, send it.
        if (player != null && emoteStr != message && emoteStr != null)
        {
            SendEntityEmote(source, emoteStr, range, nameOverride, ignoreActionBlocker, emoteVisibility: emoteVisibility); // <Onyx-EmoteVisibility-edited>
        }

        // This can happen if the entire string is sanitized out.
        if (string.IsNullOrEmpty(message))
            return;

        // This message may have a radio prefix, and should then be whispered to the resolved radio channel
        if (checkRadioPrefix)
        {
            // <Onyx-SignLanguage-edited>
            var canUseRadio = !TryComp<Content.Shared._Onyx.Language.LanguageSpeakerComponent>(source, out var speaker) ||
                              speaker.CurrentLanguage != "Sign";
            // </Onyx-SignLanguage-edited>
            if (canUseRadio && TryProcessRadioMessage(source, message, out var modMessage, out var channel))
            {
                SendEntityWhisper(source, modMessage, range, channel, nameOverride, hideLog, ignoreActionBlocker, languageOverride); // <Onyx-OSayLanguage-edited>
                return;
            }
        }

        // Otherwise, send whatever type.
        // <Onyx-CollectiveMind>
        if (desiredType == InGameICChatType.CollectiveMind)
        {
            if (TryProcessCollectiveMindMessage(source, message, out var collectiveMessage, out var collectiveChannel))
            {
                if (TryComp<Content.Shared._Onyx.CollectiveMind.CollectiveMindComponent>(source, out var collective) && collective.RespectAccents)
                    collectiveMessage = TransformSpeech(source, collectiveMessage);
                _collectiveMind.Send(source, FormattedMessage.RemoveMarkupOrThrow(collectiveMessage), collectiveChannel);
            }
            return;
        }
        // </Onyx-CollectiveMind>

        switch (desiredType)
        {
            case InGameICChatType.Speak:
                SendEntitySpeak(source, message, range, nameOverride, hideLog, ignoreActionBlocker, languageOverride); // <Onyx-OSayLanguage-edited>
                break;
            case InGameICChatType.Whisper:
                SendEntityWhisper(source, message, range, null, nameOverride, hideLog, ignoreActionBlocker, languageOverride); // <Onyx-OSayLanguage-edited>
                break;
            case InGameICChatType.Emote:
                SendEntityEmote(source, message, range, nameOverride, hideLog: hideLog, ignoreActionBlocker: ignoreActionBlocker, emoteVisibility: emoteVisibility); // <Onyx-EmoteVisibility-edited>
                break;
        }
    }

    /// <inheritdoc />
    public override void TrySendInGameOOCMessage(
        EntityUid source,
        string message,
        InGameOOCChatType type,
        bool hideChat,
        IConsoleShell? shell = null,
        ICommonSession? player = null
        )
    {
        if (!CanSendInGame(message, shell, player))
            return;

        if (player != null && _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)
            return;

        // It doesn't make any sense for a non-player to send in-game OOC messages, whereas non-players may be sending
        // in-game IC messages.
        if (player?.AttachedEntity is not { Valid: true } entity || source != entity)
            return;

        message = SanitizeInGameOOCMessage(message);

        var sendType = type;
        // If dead player LOOC is disabled, unless you are an admin with Moderator perms, send dead messages to dead chat
        if ((_adminManager.IsAdmin(player) && _adminManager.HasAdminFlag(player, AdminFlags.Moderator)) // Override if admin
            || _deadLoocEnabled
            || (!HasComp<GhostComponent>(source) && !_mobStateSystem.IsDead(source))) // Check that player is not dead
        {
        }
        else
            sendType = InGameOOCChatType.Dead;

        // If crit player LOOC is disabled, don't send the message at all.
        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        // Systems can differentiate Looc and DeadChat by type, and cancel the speak attempt if necessary.
        var ev = new InGameOocMessageAttemptEvent(player, sendType);
        RaiseLocalEvent(source, ref ev, true);
        if (ev.Cancelled)
            return;

        switch (sendType)
        {
            case InGameOOCChatType.Dead:
                SendDeadChat(source, player, message, hideChat);
                break;
            case InGameOOCChatType.Looc:
                SendLOOC(source, player, message, hideChat);
                break;
        }
    }
}

/// <summary>
///     This event is raised before chat messages are sent out to clients. This enables some systems to send the chat
///     messages to otherwise out-of view entities (e.g. for multiple viewports from cameras).
/// </summary>
public record ExpandICChatRecipientsEvent(EntityUid Source, float VoiceRange, Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> Recipients)
{
}

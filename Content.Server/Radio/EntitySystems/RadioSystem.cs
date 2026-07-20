using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Power.Components;
using Content.Server._Onyx.Chat;
using Content.Server._Onyx.Telecommunications;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed partial class RadioSystem : EntitySystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private TelecommunicationsChainSystem _telecommunications = default!;

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        var msg = args.ChatMsg;
        if (_ghost.CanGhostWarp(actor.PlayerSession, out _))
        {
            msg = new MsgChatMessage
            {
                Message = new ChatMessage(args.ChatMsg.Message)
                {
                    WrappedMessage = _chatManager.PrependFollowButtonIfAppropriate(
                        args.ChatMsg.Message.WrappedMessage,
                        args.MessageSource,
                        actor.PlayerSession.Channel),
                },
            };
        }

        _netMan.ServerSendMessage(msg, actor.PlayerSession.Channel);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioMessage(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, bool escapeMarkup = true)
    {
        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.Resolve(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceXform = Transform(radioSource);
        var sourceMapId = sourceXform.MapID;
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);
        var transmittedMessage = message;
        var canBroadcast = canSend;

        if (canBroadcast && !channel.LongRange && !sourceServerExempt)
        {
            var route = _telecommunications.RouteSignal(sourceMapId, channel, messageSource, message);
            canBroadcast = route.CanBroadcast;
            transmittedMessage = route.Message;
        }

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(transmittedMessage)
            : transmittedMessage;

        var inlineFormattedMessage = InlineActionFormatter.Format(content); // <Onyx-InlineActions>
        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", name),
            ("message", inlineFormattedMessage) // <Onyx-InlineActions>
        );

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var chat = new ChatMessage(
            ChatChannel.Radio,
            transmittedMessage,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };
        var ev = new RadioReceiveEvent(transmittedMessage, messageSource, channel, radioSource, chatMsg);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canBroadcast && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive
                && !(HasActiveTransmitter(transform.MapID) && HasActiveTransmitter(sourceMapId)))
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(chat);
        _messages.Remove(message);
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveTransmitter(MapId mapId)
    {
        return EntityQuery<TelecomServerComponent, ApcPowerReceiverComponent, TransformComponent>()
            .Any(server => server.Item3.MapID == mapId && server.Item2.Powered);
    }
}

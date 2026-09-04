using Content.Server.Chat.Systems;
using Content.Shared._Onyx.Chat;
using Content.Shared.Chat.Prototypes;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Chat;

public sealed partial class EmoteVisibilitySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;

    private const int MaxCustomEmoteLength = 512;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SendEmoteMessage>(OnSendEmote);
        SubscribeNetworkEvent<PlayCustomEmoteMessage>(OnPlayCustomEmote);
        SubscribeNetworkEvent<PlayPanelEmoteMessage>(OnPlayPanelEmote);
    }

    private void OnSendEmote(SendEmoteMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } source ||
            !Enum.IsDefined(message.Range) ||
            !Enum.IsDefined(message.Perspective))
            return;

        var options = new EmoteVisibilityOptions(
            message.Range,
            Math.Clamp(message.Radius, EmoteVisibilityOptions.MinRadius, EmoteVisibilityOptions.MaxRadius),
            message.Perspective,
            message.ShowToGhosts);
        _chat.SendEmote(source, message.Message, options, player: args.SenderSession);
    }

    private void OnPlayCustomEmote(PlayCustomEmoteMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } source ||
            !Enum.IsDefined(message.Range) ||
            !Enum.IsDefined(message.Perspective))
            return;

        var text = message.Message.Trim();
        if (text.Length == 0)
            return;
        if (text.Length > MaxCustomEmoteLength)
            text = text[..MaxCustomEmoteLength];

        var options = new EmoteVisibilityOptions(
            message.Range,
            Math.Clamp(message.Radius, EmoteVisibilityOptions.MinRadius, EmoteVisibilityOptions.MaxRadius),
            message.Perspective,
            message.ShowToGhosts);
        _chat.SendEmote(source, text, options, player: args.SenderSession);

        if (message.SoundId != null &&
            _proto.TryIndex<CustomEmoteSoundPrototype>(message.SoundId, out var sound))
            _audio.PlayPvs(sound.Sound, source);
    }

    private void OnPlayPanelEmote(PlayPanelEmoteMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } source)
            return;

        if (!_proto.TryIndex<EmotePrototype>(message.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
            return;

        var allowed = _chat.AllowedToUseEmote(source, proto);
        if (!_chat.TryEmoteWithChat(source, message.ProtoId, forceEmote: true))
            return;

        if (allowed)
            return;

        foreach (var sounds in _proto.EnumeratePrototypes<EmoteSoundsPrototype>())
        {
            if (!sounds.Sounds.ContainsKey(proto.ID))
                continue;

            _chat.TryPlayEmoteSound(source, sounds, proto);
            break;
        }
    }
}

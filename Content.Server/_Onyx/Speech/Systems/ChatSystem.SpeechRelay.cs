// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Speech;
using Content.Shared.Chat;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private SpeechRelayContext? _speechRelay;

    public override EntityUid? GetSpeechBarkSource(EntityUid source)
    {
        return _speechRelay is { } context && context.Relay == source
            ? context.BarkSource
            : source;
    }

    public override float? GetSpeechTextRevealSpeed(EntityUid source)
    {
        if (_speechRelay is { } context && context.Relay == source)
            return context.RevealSpeed;

        return TryComp<SpeechTextRevealComponent>(source, out var reveal)
            ? reveal.Speed
            : null;
    }

    public override void SendRelayedSpeech(
        EntityUid relay,
        EntityUid speaker,
        string message,
        InGameICChatType type,
        ChatTransmitRange range,
        string name,
        bool relayBarks)
    {
        var oldRelay = _speechRelay;
        _speechRelay = new SpeechRelayContext(
            relay,
            relayBarks ? speaker : null,
            TryComp<SpeechTextRevealComponent>(speaker, out var reveal) ? reveal.Speed : null);

        if (!_language.TryGetCurrentLanguage(speaker, out var language))
        {
            _speechRelay = oldRelay;
            return;
        }

        try
        {
            TrySendInGameICMessage(
                relay,
                message,
                type,
                range,
                nameOverride: name,
                checkRadioPrefix: false,
                languageOverride: language.ID);
        }
        finally
        {
            _speechRelay = oldRelay;
        }
    }

    private readonly record struct SpeechRelayContext(EntityUid Relay, EntityUid? BarkSource, float? RevealSpeed);
}

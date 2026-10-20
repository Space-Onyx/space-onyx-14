// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Onyx.Speech;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private readonly IConfigurationManager _speechRevealConfig = IoCManager.Resolve<IConfigurationManager>();
    private readonly List<RevealedChatMessage> _revealedChatMessages = new();

    private bool TryAddRevealedMessage(ChatMessage message, Color color)
    {
        if (message.RevealSpeed is not { } speed)
            return false;

        var formatted = new FormattedMessage();
        formatted.PushColor(color);
        if (!formatted.TryAddMarkup(message.WrappedMessage, out _))
            formatted.AddText(message.WrappedMessage);
        formatted.Pop();

        var range = SpeechTextReveal.FindContentRange(formatted);
        if (range.Start < 0 || range.Runes.Count == 0)
            return false;

        var initial = SpeechTextReveal.Create(formatted, 0, range);
        var entry = Contents.AddMessage(initial, tagsAllowed: null);
        var timeline = SpeechTextReveal.BuildTimeline(
            range.Runes,
            speed,
            _speechRevealConfig.GetCVar(CCVars.SpeechTextCommaPause),
            _speechRevealConfig.GetCVar(CCVars.SpeechTextSentencePause),
            _speechRevealConfig.GetCVar(CCVars.SpeechTextEllipsisPause));
        _revealedChatMessages.Add(new RevealedChatMessage(entry, formatted, range, timeline));
        return true;
    }

    private void ResetSpeechTextReveal()
    {
        _revealedChatMessages.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = _revealedChatMessages.Count - 1; i >= 0; i--)
        {
            var reveal = _revealedChatMessages[i];
            reveal.Elapsed += args.DeltaSeconds;
            var visibleRunes = SpeechTextReveal.GetVisibleRunes(reveal.Timeline, reveal.Elapsed, reveal.VisibleRunes);
            if (visibleRunes == reveal.VisibleRunes)
                continue;

            reveal.VisibleRunes = visibleRunes;
            var visible = SpeechTextReveal.Create(reveal.Message, visibleRunes, reveal.Range);
            if (!Contents.SetMessage(reveal.Entry, visible, tagsAllowed: null) ||
                visibleRunes == reveal.RuneCount)
                _revealedChatMessages.RemoveAt(i);
        }
    }

    private sealed class RevealedChatMessage(
        uint entry,
        FormattedMessage message,
        SpeechTextReveal.TextRange range,
        float[] timeline)
    {
        public readonly uint Entry = entry;
        public readonly FormattedMessage Message = message;
        public readonly SpeechTextReveal.TextRange Range = range;
        public readonly int RuneCount = range.Runes.Count;
        public readonly float[] Timeline = timeline;
        public float Elapsed;
        public int VisibleRunes;
    }
}

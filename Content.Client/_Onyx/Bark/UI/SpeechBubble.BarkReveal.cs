// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Text;
using Content.Client._Onyx.SpeechBarks;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Chat.UI;

public abstract partial class SpeechBubble
{
    private readonly List<BarkTextReveal> _barkTextReveals = new();
    private SpeechBarksSystem? _speechBarks;
    private uint _barkRevealRegistrationId;

    protected void SetBarkRevealedMessage(RichTextLabel label, FormattedMessage message)
    {
        label.SetMessage(message);

        if (RevealWithBarks)
            _barkTextReveals.Add(new BarkTextReveal(label, message));
    }

    private void InitializeBarkReveal(string message)
    {
        if (_barkTextReveals.Count == 0)
            return;

        OnBarkProgress(0f);
        _speechBarks = _entityManager.System<SpeechBarksSystem>();
        _barkRevealRegistrationId = _speechBarks.TrackSpeechBubble(_senderEntity, message, OnBarkProgress);
    }

    private void OnBarkProgress(float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);

        foreach (var reveal in _barkTextReveals)
        {
            var visibleRunes = (int) MathF.Ceiling(reveal.RuneCount * progress);
            if (visibleRunes == reveal.VisibleRunes)
                continue;

            reveal.VisibleRunes = visibleRunes;
            reveal.Label.SetMessage(CreateBarkRevealedMessage(reveal.Message, visibleRunes));
        }

        _deathTime = _timing.RealTime + TotalTime;
    }

    private static FormattedMessage CreateBarkRevealedMessage(FormattedMessage message, int visibleRunes)
    {
        var result = new FormattedMessage(message.Count);

        foreach (var node in message.Nodes)
        {
            if (node.Name != null)
            {
                if (node.Closing)
                    result.Pop();
                else
                    result.PushTag(node);

                continue;
            }

            var text = node.Value.StringValue;
            if (text == null)
                continue;

            var visible = new StringBuilder(text.Length);
            var hidden = new StringBuilder(text.Length);

            foreach (var rune in text.EnumerateRunes())
            {
                if (visibleRunes-- > 0)
                    visible.Append(rune);
                else
                    hidden.Append(rune);
            }

            result.AddText(visible.ToString());
            if (hidden.Length == 0)
                continue;

            result.PushColor(Color.Transparent);
            result.AddText(hidden.ToString());
            result.Pop();
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (_speechBarks != null)
            _speechBarks.UntrackSpeechBubble(_senderEntity, _barkRevealRegistrationId);

        base.Dispose(disposing);
    }

    private sealed class BarkTextReveal
    {
        public readonly RichTextLabel Label;
        public readonly FormattedMessage Message;
        public readonly int RuneCount;
        public int VisibleRunes = -1;

        public BarkTextReveal(RichTextLabel label, FormattedMessage message)
        {
            Label = label;
            Message = message;

            foreach (var _ in message.EnumerateRunes())
            {
                RuneCount++;
            }
        }
    }
}

// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Text;
using Content.Client._Onyx.Speech;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Chat.UI;

public abstract partial class SpeechBubble
{
    private Control _bubble = default!;
    private RevealedText? _revealedText;
    private TimeSpan _revealStart;
    private float[] _revealTimeline = [];
    private int _visibleRunes = -1;
    private bool _revealActive;

    public event Action<EntityUid, SpeechBubble, float>? OnHeightChanged;

    protected void SetRevealedMessage(RichTextLabel label, FormattedMessage message)
    {
        if (!RevealText)
        {
            label.SetMessage(message, tagsAllowed: null);
            return;
        }

        _revealedText = new RevealedText(label, message);
        _visibleRunes = 0;
        label.SetMessage(SpeechTextReveal.Create(message, 0), tagsAllowed: null);
    }

    private void InitializeTextReveal(float? speed)
    {
        if (_revealedText == null || speed == null)
            return;

        _revealTimeline = SpeechTextReveal.BuildTimeline(
            _revealedText.Runes,
            speed.Value,
            ConfigManager.GetCVar(CCVars.SpeechTextCommaPause),
            ConfigManager.GetCVar(CCVars.SpeechTextSentencePause),
            ConfigManager.GetCVar(CCVars.SpeechTextEllipsisPause));
        _revealStart = _timing.RealTime;
        _revealActive = _revealTimeline.Length > 0;
    }

    private void UpdateTextReveal(int visibleRunes)
    {
        if (_visibleRunes == visibleRunes)
            return;

        _visibleRunes = visibleRunes;
        var reveal = _revealedText!;
        var count = Math.Min(visibleRunes, reveal.RuneCount);
        reveal.Label.SetMessage(SpeechTextReveal.Create(reveal.Message, count), tagsAllowed: null);

        UpdateContentSize();
        _deathTime = _timing.RealTime + TotalTime;
        _revealActive = count < reveal.RuneCount;
    }

    private void FrameUpdateTextReveal()
    {
        if (!_revealActive)
            return;

        var elapsed = (float) (_timing.RealTime - _revealStart).TotalSeconds;
        UpdateTextReveal(SpeechTextReveal.GetVisibleRunes(_revealTimeline, elapsed, _visibleRunes));
    }

    private void UpdateContentSize()
    {
        ForceRunStyleUpdate();
        _bubble.Measure(Vector2Helpers.Infinity);
        var oldHeight = ContentSize.Y;
        ContentSize = _bubble.DesiredSize;
        var delta = ContentSize.Y - oldHeight;
        if (!MathHelper.CloseTo(delta, 0f))
            OnHeightChanged?.Invoke(SenderEntity, this, delta);
    }

    private sealed class RevealedText
    {
        public readonly RichTextLabel Label;
        public readonly FormattedMessage Message;
        public readonly List<Rune> Runes;
        public readonly int RuneCount;

        public RevealedText(RichTextLabel label, FormattedMessage message)
        {
            Label = label;
            Message = message;

            Runes = SpeechTextReveal.GetRunes(message);
            RuneCount = Runes.Count;
        }
    }
}

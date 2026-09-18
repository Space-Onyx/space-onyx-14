// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Radio;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Radio.Ui;

[UsedImplicitly]
public sealed class HandheldRadioBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private HandheldRadioMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<HandheldRadioMenu>();
        _menu.OnFrequencyEntered += frequency =>
            SendPredictedMessage(new HandheldRadioSetFrequencyMessage(frequency));
        _menu.OnFrequencyStepped += direction =>
            SendPredictedMessage(new HandheldRadioStepFrequencyMessage(direction));
        _menu.OnMicPressed += enabled =>
            SendPredictedMessage(new HandheldRadioToggleMicrophoneMessage(enabled));
        _menu.OnSpeakerPressed += enabled =>
            SendPredictedMessage(new HandheldRadioToggleSpeakerMessage(enabled));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is HandheldRadioBuiState radioState)
            _menu?.UpdateState(radioState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Close();
        _menu = null;
    }
}

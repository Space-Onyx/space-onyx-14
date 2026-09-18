// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Radio;

/// <summary>
/// BUI key for the handheld radio frequency tuner, SS13-style (BlueMoon Radio tGUI).
/// </summary>
[Serializable, NetSerializable]
public enum HandheldRadioUiKey : byte
{
    Key,
}

/// <summary>
/// Client asks the server to tune the radio to an explicit frequency.
/// The server sanitizes the value and resolves a channel by frequency.
/// </summary>
[Serializable, NetSerializable]
public sealed class HandheldRadioSetFrequencyMessage(float frequency) : BoundUserInterfaceMessage
{
    public float Frequency = frequency;
}

/// <summary>
/// Client asks the server to nudge the frequency by one step, SS13-style -/+ buttons.
/// </summary>
[Serializable, NetSerializable]
public sealed class HandheldRadioStepFrequencyMessage(int direction) : BoundUserInterfaceMessage
{
    public int Direction = direction;
}

/// <summary>
/// Client toggles the radio microphone (broadcasting), SS13-style.
/// </summary>
[Serializable, NetSerializable]
public sealed class HandheldRadioToggleMicrophoneMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

/// <summary>
/// Client toggles the radio speaker (listening), SS13-style.
/// </summary>
[Serializable, NetSerializable]
public sealed class HandheldRadioToggleSpeakerMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

/// <summary>
/// Server-authoritative snapshot for the tuner window, SS13 BlueMoon Radio tGUI style:
/// frequency dial, audio toggles and the tuned-channel indicator.
/// </summary>
[Serializable, NetSerializable]
public sealed class HandheldRadioBuiState(
    float frequency,
    bool frequencyLocked,
    float minFrequency,
    float maxFrequency,
    float step,
    bool microphoneEnabled,
    bool speakerEnabled,
    string? currentChannelName,
    string? currentChannelColor) : BoundUserInterfaceState
{
    public float Frequency = frequency;
    public bool FrequencyLocked = frequencyLocked;
    public float MinFrequency = minFrequency;
    public float MaxFrequency = maxFrequency;
    public float Step = step;
    public bool MicrophoneEnabled = microphoneEnabled;
    public bool SpeakerEnabled = speakerEnabled;
    public string? CurrentChannelName = currentChannelName;
    public string? CurrentChannelColor = currentChannelColor;
}

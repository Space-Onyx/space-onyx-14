// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Radio;
using Content.Shared._Onyx.Radio.Components;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Radio.EntitySystems;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// SS13-style (BlueMoon Radio tGUI) frequency tuner for handheld radios.
/// The frequency dial is free; the active channel is resolved from it.
/// </summary>
public sealed partial class RadioDeviceSystem
{
    [Dependency] private UserInterfaceSystem _tunerUi = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const float FrequencyTolerance = 0.001f;

    public override void Initialize()
    {
        base.Initialize();
        InitializeHandheldTuner();
    }

    private void InitializeHandheldTuner()
    {
        SubscribeLocalEvent<HandheldRadioTunerComponent, ComponentInit>(OnTunerInit);
        SubscribeLocalEvent<HandheldRadioTunerComponent, BoundUIOpenedEvent>(OnTunerUiOpened);
        SubscribeLocalEvent<HandheldRadioTunerComponent, ExaminedEvent>(OnTunerExamine);

        Subs.BuiEvents<HandheldRadioTunerComponent>(HandheldRadioUiKey.Key, subs =>
        {
            subs.Event<HandheldRadioSetFrequencyMessage>(OnTunerSetFrequency);
            subs.Event<HandheldRadioStepFrequencyMessage>(OnTunerStepFrequency);
            subs.Event<HandheldRadioToggleMicrophoneMessage>(OnTunerToggleMicrophone);
            subs.Event<HandheldRadioToggleSpeakerMessage>(OnTunerToggleSpeaker);
        });
    }

    private void OnTunerInit(Entity<HandheldRadioTunerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.MinFrequency = Math.Min(ent.Comp.MinFrequency, ent.Comp.MaxFrequency);
        ent.Comp.MaxFrequency = Math.Max(ent.Comp.MinFrequency, ent.Comp.MaxFrequency);

        if (ent.Comp.Frequency == 0 && TryComp<RadioMicrophoneComponent>(ent, out var mic)
            && ProtoMan.TryIndex<RadioChannelPrototype>(mic.BroadcastChannel, out var micProto))
        {
            ent.Comp.Frequency = (float) micProto.Frequency;
        }

        ent.Comp.Frequency = SanitizeTunerFrequency(ent.Comp, ent.Comp.Frequency);

        SyncSpeakerChannels(ent);
        Dirty(ent);
    }

    private void OnTunerUiOpened(Entity<HandheldRadioTunerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateTunerUi(ent);
    }

    private void OnTunerExamine(Entity<HandheldRadioTunerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var channel = ResolveTunerChannel(ent);
        if (channel != null && ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
        {
            args.PushMarkup(Loc.GetString("handheld-radio-component-tuner-examine",
                ("channel", proto.LocalizedName),
                ("frequency", proto.Frequency)));
        }
    }

    private void OnTunerSetFrequency(Entity<HandheldRadioTunerComponent> ent, ref HandheldRadioSetFrequencyMessage args)
    {
        if (ent.Comp.FrequencyLocked)
            return;

        TryTuneToFrequency(ent, args.Frequency, args.Actor);
    }

    private void OnTunerStepFrequency(Entity<HandheldRadioTunerComponent> ent, ref HandheldRadioStepFrequencyMessage args)
    {
        if (ent.Comp.FrequencyLocked)
            return;

        var direction = Math.Clamp(args.Direction, -1, 1);
        if (direction == 0)
            return;

        TryTuneToFrequency(ent, ent.Comp.Frequency + direction * ent.Comp.Step, args.Actor);
    }

    private void OnTunerToggleMicrophone(Entity<HandheldRadioTunerComponent> ent, ref HandheldRadioToggleMicrophoneMessage args)
    {
        if (args.Enabled && ResolveTunerChannel(ent) == null)
        {
            _popup.PopupEntity(Loc.GetString("handheld-radio-component-no-signal",
                ("frequency", ent.Comp.Frequency)), ent, args.Actor);
            UpdateTunerUi(ent);
            return;
        }

        SetMicrophoneEnabled(ent.Owner, args.Actor, args.Enabled, true);
        UpdateTunerUi(ent);
    }

    private void OnTunerToggleSpeaker(Entity<HandheldRadioTunerComponent> ent, ref HandheldRadioToggleSpeakerMessage args)
    {
        SetSpeakerEnabled(ent.Owner, args.Actor, args.Enabled, true);
        UpdateTunerUi(ent);
    }

    /// <summary>
    /// The speaker hears the dialed frequency, SS13-style.
    /// </summary>
    private void SyncSpeakerChannels(Entity<HandheldRadioTunerComponent> ent)
    {
        if (!TryComp<RadioSpeakerComponent>(ent, out var speaker))
            return;

        var tuned = FindSupportedChannelByFrequency(ent.Comp, ent.Comp.Frequency);
        speaker.Channels = tuned == null
            ? []
            : new HashSet<ProtoId<RadioChannelPrototype>> { tuned };
        Dirty(ent.Owner, speaker);

        if (speaker.Enabled)
        {
            var active = EnsureComp<ActiveRadioComponent>(ent);
            active.Channels.Clear();
            foreach (var channel in speaker.Channels)
            {
                active.Channels.Add(channel);
            }
        }
    }

    private void TryTuneToFrequency(Entity<HandheldRadioTunerComponent> ent, float frequency, EntityUid? user)
    {
        frequency = SanitizeTunerFrequency(ent.Comp, frequency);
        ent.Comp.Frequency = frequency;

        var channel = FindSupportedChannelByFrequency(ent.Comp, frequency);
        if (channel != null && TryComp<RadioMicrophoneComponent>(ent, out var mic))
        {
            mic.BroadcastChannel = channel;
            Dirty(ent.Owner, mic);

            if (user != null && ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
            {
                _popup.PopupEntity(Loc.GetString("handheld-radio-component-channel-set",
                    ("channel", proto.LocalizedName)), ent, user.Value);
            }
        }

        if (channel == null)
            SetMicrophoneEnabled(ent.Owner, user, false, true);

        SyncSpeakerChannels(ent);

        if (channel == null && user != null)
        {
            _popup.PopupEntity(Loc.GetString("handheld-radio-component-no-signal",
                ("frequency", frequency)), ent, user.Value);
        }

        Dirty(ent);
        UpdateTunerUi(ent);
    }

    private string? ResolveTunerChannel(Entity<HandheldRadioTunerComponent> ent)
    {
        return FindSupportedChannelByFrequency(ent.Comp, ent.Comp.Frequency);
    }

    private string? FindSupportedChannelByFrequency(HandheldRadioTunerComponent component, float frequency)
    {
        foreach (var channel in component.SupportedChannels)
        {
            if (!ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
                continue;

            if (Math.Abs((float) proto.Frequency - frequency) < FrequencyTolerance)
                return channel;
        }

        return null;
    }

    private static float SanitizeTunerFrequency(HandheldRadioTunerComponent component, float frequency)
    {
        frequency = Math.Clamp(frequency, component.MinFrequency, component.MaxFrequency);
        return MathF.Round(frequency * 10f) / 10f;
    }

    private void UpdateTunerUi(Entity<HandheldRadioTunerComponent> ent)
    {
        string? currentName = null;
        string? currentColor = null;

        var current = FindSupportedChannelByFrequency(ent.Comp, ent.Comp.Frequency);
        if (current != null && ProtoMan.TryIndex<RadioChannelPrototype>(current, out var currentProto))
        {
            currentName = currentProto.LocalizedName;
            currentColor = currentProto.Color.ToHex();
        }

        var micEnabled = TryComp<RadioMicrophoneComponent>(ent, out var mic) && mic.Enabled;
        var speakerEnabled = TryComp<RadioSpeakerComponent>(ent, out var speaker) && speaker.Enabled;

        _tunerUi.SetUiState(ent.Owner, HandheldRadioUiKey.Key, new HandheldRadioBuiState(
            ent.Comp.Frequency,
            ent.Comp.FrequencyLocked,
            ent.Comp.MinFrequency,
            ent.Comp.MaxFrequency,
            ent.Comp.Step,
            micEnabled,
            speakerEnabled,
            currentName,
            currentColor));
    }
}

using Content.Server._Onyx.Radio.Components; // <Onyx-Radio>
using Content.Shared.Chat; // <Onyx-StationRadio>
using Content.Shared.Examine; // <Onyx-Radio>
using Content.Shared.Popups; // <Onyx-Radio>
using Content.Shared.Power.EntitySystems; // <Onyx-StationRadio>
using Content.Shared.Radio; // <Onyx-StationRadio>
using Content.Shared.Radio.Components; // <Onyx-Radio>
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Speech; // <Onyx-StationRadio>
using Content.Shared.Verbs; // <Onyx-Radio>
using Robust.Shared.Prototypes; // <Onyx-Radio>

namespace Content.Server.Radio.EntitySystems;

/// <inheritdoc/>
public sealed partial class RadioDeviceSystem : SharedRadioDeviceSystem // <Onyx-Radio-edited>
// <Onyx-Radio>
{
    [Dependency] private SharedPopupSystem _popup = default!; // <Onyx-Radio>

    [SubscribeLocalEvent]
    private void OnHandheldPresetInit(Entity<HandheldRadioPresetComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Channels.Count == 0)
            return;

        ent.Comp.CurrentIndex = Math.Clamp(ent.Comp.CurrentIndex, 0, ent.Comp.Channels.Count - 1);
        SetHandheldPresetChannel(ent, ent.Comp.CurrentIndex, null, true);
    }

    [SubscribeLocalEvent]
    private void OnHandheldPresetExamine(Entity<HandheldRadioPresetComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Channels.Count == 0)
            return;

        var channel = GetHandheldPresetChannel(ent.Comp);
        if (channel == null || !ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
            return;

        args.PushMarkup(Loc.GetString("handheld-radio-component-preset-examine",
            ("channel", proto.LocalizedName),
            ("frequency", proto.Frequency)));
    }

    [SubscribeLocalEvent]
    private void OnHandheldPresetVerbs(Entity<HandheldRadioPresetComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess || ent.Comp.Channels.Count == 0)
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Channels.Count; i++)
        {
            var channel = ent.Comp.Channels[i];
            if (!ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
                continue;

            var index = i;
            var selected = index == ent.Comp.CurrentIndex;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("handheld-radio-component-preset-verb",
                    ("channel", proto.LocalizedName),
                    ("frequency", proto.Frequency)),
                Category = VerbCategory.PowerLevel,
                Disabled = selected,
                Message = selected ? Loc.GetString("handheld-radio-component-preset-current") : null,
                Act = () => SetHandheldPresetChannel(ent, index, user),
            });
        }
    }

    private string? GetHandheldPresetChannel(HandheldRadioPresetComponent component)
    {
        if (component.Channels.Count == 0)
            return null;

        component.CurrentIndex = Math.Clamp(component.CurrentIndex, 0, component.Channels.Count - 1);
        return component.Channels[component.CurrentIndex];
    }

    private void SetHandheldPresetChannel(
        Entity<HandheldRadioPresetComponent> ent,
        int index,
        EntityUid? user,
        bool quiet = false)
    {
        if (ent.Comp.Channels.Count == 0)
            return;

        index = Math.Clamp(index, 0, ent.Comp.Channels.Count - 1);
        var channel = ent.Comp.Channels[index];
        if (!ProtoMan.TryIndex<RadioChannelPrototype>(channel, out var proto))
            return;

        ent.Comp.CurrentIndex = index;

        if (TryComp<RadioMicrophoneComponent>(ent, out var mic))
        {
            mic.BroadcastChannel = channel;
            Dirty(ent, mic);
        }

        if (TryComp<RadioSpeakerComponent>(ent, out var speaker))
        {
            speaker.Channels = [channel];
            Dirty(ent, speaker);

            if (speaker.Enabled)
                EnsureComp<ActiveRadioComponent>(ent).Channels = [channel];
        }

        if (!quiet && user != null)
        {
            _popup.PopupEntity(Loc.GetString("handheld-radio-component-channel-set",
                ("channel", proto.LocalizedName)), ent, user.Value);
        }
    }
}
// </Onyx-Radio>

// <Onyx-StationRadio>
public sealed partial class StationRadioSpeakerSystem : EntitySystem
{
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    [SubscribeLocalEvent]
    private void OnReceiveAttempt(Entity<RadioSpeakerComponent> ent, ref RadioReceiveAttemptEvent args)
    {
        if (ent.Comp.PowerRequired && !_power.IsPowered(ent.Owner))
            args.Cancelled = true;
    }

}
// </Onyx-StationRadio>

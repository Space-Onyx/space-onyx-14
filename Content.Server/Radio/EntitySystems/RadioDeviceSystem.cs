using Content.Shared.Power.EntitySystems; // <Onyx-StationRadio>
using Content.Shared.Radio; // <Onyx-StationRadio>
using Content.Shared.Radio.Components; // <Onyx-StationRadio>
using Content.Shared.Radio.EntitySystems;

namespace Content.Server.Radio.EntitySystems;

/// <inheritdoc/>
public sealed partial class RadioDeviceSystem : SharedRadioDeviceSystem // <Onyx-Radio-edited>
{
    // SS13-style frequency tuner lives in Content.Server/_Onyx/Radio/RadioDeviceSystem.Tuner.cs.
}

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

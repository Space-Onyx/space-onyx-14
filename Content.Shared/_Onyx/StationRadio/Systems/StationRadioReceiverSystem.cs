using Content.Shared._Onyx.StationRadio.Components;
using Content.Shared._Onyx.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if (comp.SoundEntity != null)
            _audio.SetGain(comp.SoundEntity, args.Powered && comp.Active ? comp.DefaultParams.Volume : 0f);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
        if (comp.SoundEntity != null && _power.IsPowered(uid))
            _audio.SetGain(comp.SoundEntity, comp.Active ? comp.DefaultParams.Volume : 0f);
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, comp.DefaultParams);
        if (audio == null)
            return;

        comp.SoundEntity = audio.Value.Entity;
        Dirty(uid, comp);
        if (!_power.IsPowered(uid) || !comp.Active)
            _audio.SetGain(comp.SoundEntity, 0);
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
        Dirty(uid, comp);
    }
}

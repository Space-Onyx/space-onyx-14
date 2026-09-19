using Content.Shared.CCVar;
using Content.Shared.VoiceMask;
using Content.Shared._Onyx.SpeechBarks;
using Robust.Shared.Configuration;
using Content.Shared.Inventory;
using Content.Shared.Implants;

namespace Content.Server.VoiceMask;

public partial class VoiceMaskSystem
{
    private void InitializeBarks()
    {
        SubscribeLocalEvent<VoiceMaskComponent, InventoryRelayedEvent<TransformSpeakerBarkEvent>>(OnSpeakerVoiceTransform);
        SubscribeLocalEvent<VoiceMaskComponent, ImplantRelayEvent<TransformSpeakerBarkEvent>>(OnSpeakerVoiceTransformImplant);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeBarkMessage>(OnChangeBark);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeBarkPitchMessage>(OnChangePitch);
    }

    private void OnSpeakerVoiceTransform(EntityUid uid, VoiceMaskComponent component, ref InventoryRelayedEvent<TransformSpeakerBarkEvent> args)
    {
        if (!component.Active)
            return;

        TransformBark(component, args.Args);
    }

    private void OnSpeakerVoiceTransformImplant(EntityUid uid, VoiceMaskComponent component, ref ImplantRelayEvent<TransformSpeakerBarkEvent> args)
    {
        if (!component.Active)
            return;

        TransformBark(component, args.Args);
    }

    private void TransformBark(VoiceMaskComponent component, TransformSpeakerBarkEvent args)
    {
        if (!ProtoMan.HasIndex<BarkPrototype>(component.BarkId))
            return;

        args.Data.Pitch = Math.Clamp(component.BarkPitch, _cfgManager.GetCVar(CCVars.BarksMinPitch), _cfgManager.GetCVar(CCVars.BarksMaxPitch));
        args.Data.MinVar = Math.Clamp(component.MinVar, _cfgManager.GetCVar(CCVars.BarksMinDelay), _cfgManager.GetCVar(CCVars.BarksMaxDelay));
        args.Data.MaxVar = Math.Clamp(component.MaxVar, _cfgManager.GetCVar(CCVars.BarksMinDelay), _cfgManager.GetCVar(CCVars.BarksMaxDelay));
        args.Data.Proto = component.BarkId;
    }

    private void OnChangeBark(EntityUid uid, VoiceMaskComponent component, VoiceMaskChangeBarkMessage message)
    {
        if (!ProtoMan.HasIndex<BarkPrototype>(message.Proto))
        {
            _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-invalid"), uid);
            return;
        }

        component.BarkId = message.Proto;
        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), uid);
        UpdateUI((uid, component));
    }

    private void OnChangePitch(EntityUid uid, VoiceMaskComponent component, VoiceMaskChangeBarkPitchMessage message)
    {
        if (!float.TryParse(message.Pitch, out var pitchValue))
        {
            _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-invalid-pitch"), uid);
            return;
        }

        component.BarkPitch = pitchValue;
        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), uid);
        UpdateUI((uid, component));
    }
}

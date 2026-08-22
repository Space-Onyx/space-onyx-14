using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Content.Shared._Onyx.SpeechBarks;
using Content.Server.Chat.Systems;
using Robust.Shared.Configuration;
using Content.Shared._Onyx.CCVar;
using Content.Server.Mind;
using Content.Server.Radio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Onyx.SpeechBarks;

public sealed partial class SpeechBarksSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    private bool _isEnabled = false;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(ADTCCVars.BarksEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<SpeechBarksComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<WearingHeadsetComponent, HeadsetRadioReceiveRelayEvent>(OnHeadsetRadioReceive);
        SubscribeLocalEvent<ActiveRadioComponent, RadioReceiveEvent>(OnRadioReceive);
    }

    private void OnEntitySpoke(EntityUid uid, SpeechBarksComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled)
            return;

        var ev = new TransformSpeakerBarkEvent(uid, component.Data.Copy());
        RaiseLocalEvent(uid, ev);

        if (!TryGetBarkData(ev.Data, out var soundSpecifier, out var pitch, out var minVar, out var maxVar))
            return;

        var message = args.Message;

        foreach (var ent in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 10f))
        {
            if (!_mind.TryGetMind(ent, out _, out var mind) || mind.UserId == null || !_player.TryGetSessionById(mind.UserId, out var session))
                continue;

            RaiseNetworkEvent(new PlaySpeechBarksEvent(
                        GetNetEntity(uid),
                        message,
                        soundSpecifier,
                        pitch,
                        minVar,
                        maxVar,
                        args.ObfuscatedMessage != null), session);
        }
    }

    private void OnHeadsetRadioReceive(Entity<WearingHeadsetComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        if (!TryComp(ent.Owner, out ActorComponent? actor))
            return;

        SendRadioBark(args.RelayedEvent, ent.Comp.Headset, actor.PlayerSession);
    }

    private void OnRadioReceive(Entity<ActiveRadioComponent> ent, ref RadioReceiveEvent args)
    {
        // Headsets relay separately to their wearer. Only world radio speakers emit positional barks.
        if (!TryComp<RadioSpeakerComponent>(ent, out var speaker) || !speaker.Enabled)
            return;

        foreach (var listener in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 10f))
        {
            if (!_mind.TryGetMind(listener, out _, out var mind) || mind.UserId == null || !_player.TryGetSessionById(mind.UserId, out var session))
                continue;

            SendRadioBark(args, ent, session);
        }
    }

    private void SendRadioBark(RadioReceiveEvent args, EntityUid emitter, ICommonSession session)
    {
        if (!_isEnabled)
            return;

        var source = args.MessageSource;
        if (!TryComp(source, out SpeechBarksComponent? component))
            return;

        var ev = new TransformSpeakerBarkEvent(source, component.Data.Copy());
        RaiseLocalEvent(source, ev);

        if (!TryGetBarkData(ev.Data, out var soundSpecifier, out var pitch, out var minVar, out var maxVar))
            return;

        RaiseNetworkEvent(new PlaySpeechBarksEvent(
            GetNetEntity(source),
            args.Message,
            soundSpecifier,
            pitch,
            minVar,
            maxVar,
            false,
            true,
            GetNetEntity(emitter)), session);
    }

    private bool TryGetBarkData(BarkData data, out SoundSpecifier sound, out float pitch, out float minVar, out float maxVar)
    {
        sound = default!;
        pitch = Math.Clamp(data.Pitch, _cfg.GetCVar(ADTCCVars.BarksMinPitch), _cfg.GetCVar(ADTCCVars.BarksMaxPitch));
        minVar = Math.Clamp(data.MinVar, _cfg.GetCVar(ADTCCVars.BarksMinDelay), _cfg.GetCVar(ADTCCVars.BarksMaxDelay));
        maxVar = Math.Clamp(data.MaxVar, _cfg.GetCVar(ADTCCVars.BarksMinDelay), _cfg.GetCVar(ADTCCVars.BarksMaxDelay));
        if (minVar > maxVar)
            (minVar, maxVar) = (maxVar, minVar);

        if (!_proto.TryIndex<BarkPrototype>(data.Proto, out var proto))
            return false;

        sound = proto.Sound;
        return true;
    }
}

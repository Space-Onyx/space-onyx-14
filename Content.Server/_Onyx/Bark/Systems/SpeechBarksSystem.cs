using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Content.Shared._Onyx.SpeechBarks;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
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
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private Content.Server._Onyx.Language.LanguageSystem _languages = default!;

    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    private bool _isEnabled = false;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCVars.BarksEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<WearingHeadsetComponent, HeadsetRadioReceiveRelayEvent>(OnHeadsetRadioReceive);
        SubscribeLocalEvent<ActiveRadioComponent, RadioReceiveEvent>(OnRadioReceive,
            before: [typeof(HeadsetSystem), typeof(RadioDeviceSystem), typeof(RadioSystem)]);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        if (!_isEnabled || !_languages.TryResolveLanguage(args.Source, args.Language, out var language) || language.RequiresSight ||
            _chat.GetSpeechBarkSource(args.Source) is not { } source ||
            !TryComp<SpeechBarksComponent>(source, out var component))
            return;

        var ev = new TransformSpeakerBarkEvent(source, component.Data.Copy());
        RaiseLocalEvent(source, ev);

        if (!TryGetBarkData(ev.Data, out var soundSpecifier, out var pitch, out var minVar, out var maxVar))
            return;

        var message = args.Message;

        foreach (var ent in _lookup.GetEntitiesInRange(Transform(args.Source).Coordinates, 10f))
        {
            if (!_chat.CanHearSource(ent, args.Source))
                continue;
            if (!_mind.TryGetMind(ent, out _, out var mind) || mind.UserId == null || !_player.TryGetSessionById(mind.UserId, out var session))
                continue;

            RaiseNetworkEvent(new PlaySpeechBarksEvent(
                        GetNetEntity(args.Source),
                         _languages.CanUnderstand(ent, language.ID) ? message : _languages.Obfuscate(message, language),
                        soundSpecifier,
                        pitch,
                        minVar,
                        maxVar,
                        args.ObfuscatedMessage != null), session);
        }
    }

    private void OnHeadsetRadioReceive(Entity<WearingHeadsetComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        if (!TryComp(ent.Owner, out ActorComponent? actor) ||
            !TryGetRadioBark(args.RelayedEvent, out var sound, out var pitch, out var minVar, out var maxVar))
            return;

        SendRadioBark(args.RelayedEvent, ent.Comp.Headset, actor.PlayerSession, sound, pitch, minVar, maxVar);
    }

    private void OnRadioReceive(Entity<ActiveRadioComponent> ent, ref RadioReceiveEvent args)
    {
        ApplyRadioSpeechTextReveal(args);

        // Headsets relay separately to their wearer. Only world radio speakers emit positional barks.
        if (!TryComp<RadioSpeakerComponent>(ent, out var speaker) ||
            !speaker.Enabled ||
            !TryGetRadioBark(args, out var sound, out var pitch, out var minVar, out var maxVar))
            return;

        foreach (var listener in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 10f))
        {
            if (!_mind.TryGetMind(listener, out _, out var mind) || mind.UserId == null || !_player.TryGetSessionById(mind.UserId, out var session))
                continue;

            SendRadioBark(args, ent, session, sound, pitch, minVar, maxVar);
        }
    }

    private bool TryGetRadioBark(
        RadioReceiveEvent args,
        out SoundSpecifier sound,
        out float pitch,
        out float minVar,
        out float maxVar)
    {
        sound = default!;
        pitch = default;
        minVar = default;
        maxVar = default;
        if (!_isEnabled)
            return false;

        var source = args.MessageSource;
        if (!TryComp(source, out SpeechBarksComponent? component))
            return false;

        var ev = new TransformSpeakerBarkEvent(source, component.Data.Copy());
        RaiseLocalEvent(source, ev);
        return TryGetBarkData(ev.Data, out sound, out pitch, out minVar, out maxVar);
    }

    private void SendRadioBark(
        RadioReceiveEvent args,
        EntityUid emitter,
        ICommonSession session,
        SoundSpecifier sound,
        float pitch,
        float minVar,
        float maxVar)
    {
        RaiseNetworkEvent(new PlaySpeechBarksEvent(
            GetNetEntity(args.MessageSource),
            args.Message,
            sound,
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
        pitch = Math.Clamp(data.Pitch, _cfg.GetCVar(CCVars.BarksMinPitch), _cfg.GetCVar(CCVars.BarksMaxPitch));
        minVar = Math.Clamp(data.MinVar, _cfg.GetCVar(CCVars.BarksMinDelay), _cfg.GetCVar(CCVars.BarksMaxDelay));
        maxVar = Math.Clamp(data.MaxVar, _cfg.GetCVar(CCVars.BarksMinDelay), _cfg.GetCVar(CCVars.BarksMaxDelay));
        if (minVar > maxVar)
            (minVar, maxVar) = (maxVar, minVar);

        if (!_proto.TryIndex<BarkPrototype>(data.Proto, out var proto))
            return false;

        sound = proto.Sound;
        return true;
    }
}

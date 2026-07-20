using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._Onyx.SpeechBarks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Client.Player;
using Content.Shared._Onyx.CCVar;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Client.Audio;
using System.Text;

namespace Content.Client._Onyx.SpeechBarks;

public sealed partial class SpeechBarksSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float MinimalVolume = -10f;
    private const float WhisperFade = 4f;
    private float _volume = 0.0f;
    private float _radioVolume = 0.25f;

    private List<ActiveBark> _activeBarks = new();

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(ADTCCVars.BarksVolume, OnVolumeChanged, true);
        _cfg.OnValueChanged(ADTCCVars.BarksRadioVolume, OnRadioVolumeChanged, true);

        SubscribeNetworkEvent<PlaySpeechBarksEvent>(OnEntitySpoke);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(ADTCCVars.BarksVolume, OnVolumeChanged);
        _cfg.UnsubValueChanged(ADTCCVars.BarksRadioVolume, OnRadioVolumeChanged);
    }

    private void OnVolumeChanged(float volume)
        => _volume = volume;

    private void OnRadioVolumeChanged(float volume)
        => _radioVolume = volume;

    private float AdjustVolume(string message, bool isWhisper, bool isRadio, float volumeScale = 1f)
    {
        var volume = isWhisper ? _volume - WhisperFade : _volume;

        if (message.EndsWith("!"))
            volume += 1.5f;

        var radioVolume = isRadio ? _radioVolume : 1f;
        var effectiveVolumeScale = volumeScale * radioVolume;

        if (isRadio && effectiveVolumeScale <= 0f)
            return float.NegativeInfinity;

        return MinimalVolume + SharedAudioSystem.GainToVolume(volume) + SharedAudioSystem.GainToVolume(effectiveVolumeScale);
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? 5 : 10;
    }


    private void OnEntitySpoke(PlaySpeechBarksEvent ev)
    {
        if (!_cfg.GetCVar(ADTCCVars.ReplaceTTSWithBarks))
            return;

        if (ev.Message == null)
            return;

        EntityUid? source = null;
        var emitter = ev.IsRadio ? ev.Emitter : ev.Source;
        if (emitter != null)
        {
            if (!TryGetEntity(emitter, out var sourceUid) || Transform(sourceUid.Value).MapID == MapId.Nullspace)
                return;

            source = sourceUid;
        }

        if (ev.Source != null)
        {
            if (ev.IsRadio)
            {
                _activeBarks.RemoveAll(bark => bark.Speaker == ev.Source && bark.IsRadio);
            }
            else
            {
                _activeBarks.RemoveAll(bark => bark.Speaker == ev.Source && !bark.IsRadio);
            }
        }

        var barkMessage = RemoveInlineActions(ev.Message);

        if (string.IsNullOrWhiteSpace(barkMessage))
            return;

        var lowVar = MathF.Min(ev.LowVar, ev.HighVar);
        var highVar = MathF.Max(ev.LowVar, ev.HighVar);
        var bark = new ActiveBark(source,
                                  ev.Source,
                                  ev.SoundSpecifier,
                                  AdjustVolume(barkMessage, ev.IsWhisper, ev.IsRadio, ev.VolumeScale),
                                  ev.Pitch,
                                  AdjustDistance(ev.IsWhisper),
                                   (lowVar, highVar),

                                  barkMessage.Length / 3 + 1,
                                  ev.IsRadio);
        _activeBarks.Add(bark);
    }

    private static string RemoveInlineActions(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        StringBuilder? builder = null;
        var cursor = 0;
        var searchStart = 0;

        while (searchStart < message.Length)
        {
            var openIndex = message.IndexOf('*', searchStart);
            if (openIndex == -1)
                break;

            var closeIndex = message.IndexOf('*', openIndex + 1);
            if (closeIndex == -1)
                break;

            if (!IsActionBounded(message, openIndex, closeIndex))
            {
                searchStart = openIndex + 1;
                continue;
            }

            if (!HasVisibleText(message, openIndex + 1, closeIndex))
            {
                searchStart = closeIndex + 1;
                continue;
            }

            builder ??= new StringBuilder(message.Length);
            if (openIndex > cursor)
                builder.Append(message, cursor, openIndex - cursor);

            cursor = closeIndex + 1;
            searchStart = cursor;
        }

        if (builder == null)
            return message;

        if (cursor < message.Length)
            builder.Append(message, cursor, message.Length - cursor);

        return builder.ToString().Trim();
    }

    private static bool HasVisibleText(string text, int start, int endExclusive)
    {
        for (var i = start; i < endExclusive; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                return true;
        }

        return false;
    }

    private static bool IsActionBounded(string message, int openIndex, int closeIndex)
    {
        if (closeIndex <= openIndex + 1)
            return false;

        var leftBoundary = openIndex == 0
                           || char.IsWhiteSpace(message[openIndex - 1])
                           || char.IsPunctuation(message[openIndex - 1]);

        var rightBoundary = closeIndex == message.Length - 1
                            || char.IsWhiteSpace(message[closeIndex + 1])
                            || char.IsPunctuation(message[closeIndex + 1]);

        return leftBoundary && rightBoundary;
    }

    public void PlayDataPreview(string protoId, float pitch, float lowVar, float highVar)
    {
        if (!_proto.TryIndex<BarkPrototype>(protoId, out var proto))
            return;

        var minDelay = MathF.Min(lowVar, highVar);
        var maxDelay = MathF.Max(lowVar, highVar);
        var bark = new ActiveBark(null,
                                  null,
                                  proto.Sound,
                                  AdjustVolume("Test message", false, false),
                                  pitch,
                                  AdjustDistance(false),

                                   (minDelay, maxDelay),
                                  9,
                                  false);
        _activeBarks.Add(bark);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalSession == null)
            return;

        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            var item = _activeBarks[i];

            if (item.NextSound > _timing.CurTime)
                continue;

            if (item.BarksPlayed >= item.Length)
            {
                _activeBarks.Remove(item);
                continue;
            }

            var audioParams = AudioParams.Default.WithPitchScale(item.Pitch).WithVolume(item.Volume).WithMaxDistance(item.Distance);
            item.BarksPlayed++;
            item.NextSound = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(item.DelayVariation.Item1, item.DelayVariation.Item2));

            if (item.Source == null)
            {
                if (item.HasSource)
                    _activeBarks.Remove(item);
                else
                    _audio.PlayGlobal(_audio.ResolveSound(item.Sound), _player.LocalSession, audioParams);

                continue;
            }

            if (item.IsRadio)
            {
                _audio.PlayEntity(_audio.ResolveSound(item.Sound), _player.LocalSession, item.Source.Value, audioParams);
                continue;
            }

            if (_player.LocalEntity is { Valid: true } player)
            {
                if (item.Source == _player.LocalEntity)
                    _audio.PlayGlobal(_audio.ResolveSound(item.Sound), player, audioParams);
                else
                    _audio.PlayEntity(_audio.ResolveSound(item.Sound), _player.LocalSession, item.Source.Value, audioParams);
            }
            else
            {
                _activeBarks.Remove(item);
                continue;
            }
        }
    }

    private sealed class ActiveBark
    {
        public readonly EntityUid? Source;
        public readonly NetEntity? Speaker;
        public readonly SoundSpecifier Sound = default!;
        public readonly float Volume = default!;
        public readonly float Pitch = default!;
        public readonly float Distance = default!;
        public readonly (float, float) DelayVariation = default!;
        public readonly int Length = default!;
        public readonly bool HasSource;
        public readonly bool IsRadio;

        public TimeSpan NextSound = TimeSpan.Zero;
        public int BarksPlayed = 0;

        public ActiveBark(EntityUid? source, NetEntity? speaker, SoundSpecifier sound, float volume, float pitch, float distance, (float, float) delay, int length, bool isRadio)
        {
            Source = source;
            Speaker = speaker;
            HasSource = source.HasValue;
            IsRadio = isRadio;
            Sound = sound;
            Volume = volume;
            Pitch = pitch;
            Distance = distance;
            DelayVariation = delay;
            Length = length;
        }
    }
}

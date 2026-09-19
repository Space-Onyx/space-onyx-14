using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._Onyx.SpeechBarks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Client.Player;
using Content.Shared.CCVar;
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
    private const float ShoutVolume = 1.5f;
    private const float CommaPauseMultiplier = 1.35f;
    private const float SentencePauseMultiplier = 1.7f;
    private const float EllipsisPauseMultiplier = 2.5f;
    private const int RunesPerBark = 3;
    private const string PreviewMessage = "Test, message... Test!";
    private float _volume = 0.0f;
    private float _radioVolume = 0.25f;

    private List<ActiveBark> _activeBarks = new();
    private readonly Dictionary<EntityUid, SpeechBubbleReveal> _speechBubbleReveals = new();
    private uint _nextSpeechBubbleRevealId;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCVars.BarksVolume, OnVolumeChanged, true);
        _cfg.OnValueChanged(CCVars.BarksRadioVolume, OnRadioVolumeChanged, true);

        SubscribeNetworkEvent<PlaySpeechBarksEvent>(OnEntitySpoke);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.BarksVolume, OnVolumeChanged);
        _cfg.UnsubValueChanged(CCVars.BarksRadioVolume, OnRadioVolumeChanged);
        _activeBarks.Clear();
        _speechBubbleReveals.Clear();
    }

    private void OnVolumeChanged(float volume)
        => _volume = volume;

    private void OnRadioVolumeChanged(float volume)
        => _radioVolume = volume;

    private float AdjustVolume(bool isShouting, bool isWhisper, bool isRadio, float volumeScale = 1f)
    {
        var volume = isWhisper ? _volume - WhisperFade : _volume;

        if (isShouting)
            volume += ShoutVolume;

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
        if (!_cfg.GetCVar(CCVars.ReplaceTTSWithBarks))
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
        var isShouting = IsShouting(barkMessage);
        var speechSteps = BuildSpeechSteps(barkMessage);
        var bark = new ActiveBark(source,
                                  ev.Source,
                                  ev.SoundSpecifier,
                                  AdjustVolume(isShouting, ev.IsWhisper, ev.IsRadio, ev.VolumeScale),
                                  ev.Pitch,
                                  AdjustDistance(ev.IsWhisper),
                                   (lowVar, highVar),

                                  speechSteps,
                                  ev.IsRadio,
                                  ev.Message,
                                  isShouting);
        _activeBarks.Add(bark);

        if (source != null &&
            !ev.IsRadio &&
            _speechBubbleReveals.TryGetValue(source.Value, out var reveal) &&
            reveal.Message == ev.Message)
        {
            bark.Reveal = reveal.Update;
            reveal.Update(0f);
        }
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

    private static bool IsShouting(string message)
    {
        if (message.EndsWith('!'))
            return true;

        var letters = 0;
        var upper = 0;
        foreach (var rune in message.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
                continue;

            letters++;
            if (Rune.IsUpper(rune))
                upper++;
        }

        return letters >= 4 && upper >= letters * 2 / 3;
    }

    private static List<SpeechStep> BuildSpeechSteps(string message)
    {
        var runes = new List<Rune>();
        foreach (var rune in message.EnumerateRunes())
            runes.Add(rune);

        var steps = new List<SpeechStep>();
        var speechRunes = 0;

        for (var i = 0; i < runes.Count; i++)
        {
            var rune = runes[i];
            if (Rune.IsLetterOrDigit(rune))
            {
                speechRunes++;
                if (speechRunes < RunesPerBark)
                    continue;

                steps.Add(new SpeechStep(true, (float) (i + 1) / runes.Count, 1f, SpeechPause.None));
                speechRunes = 0;
                continue;
            }

            if (!IsPausePunctuation(rune))
                continue;

            if (speechRunes > 0)
            {
                steps.Add(new SpeechStep(true, (float) i / runes.Count, 1f, SpeechPause.None));
                speechRunes = 0;
            }

            var pauseMultiplier = GetPauseMultiplier(rune);
            var pause = rune.Value is ',' or ';' or ':' ? SpeechPause.Comma : SpeechPause.Sentence;
            var consecutiveDots = rune.Value == '.' ? 1 : 0;
            var maxConsecutiveDots = consecutiveDots;
            while (i + 1 < runes.Count && IsPausePunctuation(runes[i + 1]))
            {
                i++;
                pauseMultiplier = Math.Max(pauseMultiplier, GetPauseMultiplier(runes[i]));
                if (runes[i].Value == '.')
                {
                    consecutiveDots++;
                    maxConsecutiveDots = Math.Max(maxConsecutiveDots, consecutiveDots);
                }
                else
                {
                    consecutiveDots = 0;
                }

                if (runes[i].Value is '.' or '?' or '!')
                    pause = SpeechPause.Sentence;
            }

            if (maxConsecutiveDots >= 3)
            {
                pauseMultiplier = EllipsisPauseMultiplier;
                pause = SpeechPause.Ellipsis;
            }

            steps.Add(new SpeechStep(false, (float) (i + 1) / runes.Count, pauseMultiplier, pause));
        }

        if (speechRunes > 0 || steps.Count == 0)
            steps.Add(new SpeechStep(speechRunes > 0, 1f, 1f, SpeechPause.None));
        else if (steps[^1].RevealProgress < 1f)
            steps.Add(new SpeechStep(false, 1f, 0f, SpeechPause.None));

        return steps;
    }

    private static bool IsPausePunctuation(Rune rune)
    {
        return rune.Value is ',' or ';' or ':' or '.' or '?' or '!';
    }

    private static float GetPauseMultiplier(Rune rune)
    {
        return rune.Value is ',' or ';' or ':' ? CommaPauseMultiplier : SentencePauseMultiplier;
    }

    public void PlayDataPreview(string protoId, float pitch, float lowVar, float highVar)
    {
        if (!_proto.TryIndex<BarkPrototype>(protoId, out var proto))
            return;

        var minDelay = MathF.Min(lowVar, highVar);
        var maxDelay = MathF.Max(lowVar, highVar);
        var speechSteps = BuildSpeechSteps(PreviewMessage);
        var bark = new ActiveBark(null,
                                  null,
                                  proto.Sound,
                                  AdjustVolume(false, false, false),
                                  pitch,
                                  AdjustDistance(false),

                                   (minDelay, maxDelay),
                                  speechSteps,
                                  false,
                                  isShouting: false);
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

            if (item.StepIndex >= item.Steps.Count)
            {
                _activeBarks.Remove(item);
                continue;
            }

            var step = item.Steps[item.StepIndex++];
            item.Reveal?.Invoke(step.RevealProgress);
            item.NextSound = _timing.CurTime + TimeSpan.FromSeconds(GetNextDelay(item, step));

            if (!step.PlaySound)
                continue;

            var pitch = item.IsShouting ? item.Pitch * 1.05f : item.Pitch;
            var audioParams = AudioParams.Default.WithPitchScale(pitch).WithVolume(item.Volume).WithMaxDistance(item.Distance);

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

    private float GetNextDelay(ActiveBark bark, SpeechStep step)
    {
        var delay = _random.NextFloat(bark.DelayVariation.Item1, bark.DelayVariation.Item2);
        var maxDuration = Math.Max(0.1f, _cfg.GetCVar(CCVars.BarksMaxSpeechDuration));
        delay = Math.Min(delay, maxDuration / bark.TotalDelayWeight);

        if (bark.IsShouting && step.PlaySound)
            delay *= Math.Clamp(_cfg.GetCVar(CCVars.BarksShoutSpeedMultiplier), 0.1f, 1f);

        delay *= step.DelayWeight;
        var minimumPause = step.Pause switch
        {
            SpeechPause.Comma => _cfg.GetCVar(CCVars.BarksCommaPause),
            SpeechPause.Sentence => _cfg.GetCVar(CCVars.BarksSentencePause),
            SpeechPause.Ellipsis => _cfg.GetCVar(CCVars.BarksEllipsisPause),
            _ => 0f,
        };

        return Math.Max(delay, minimumPause);
    }

    public bool CanRevealSpeechBubble(EntityUid speaker)
    {
        return _cfg.GetCVar(CCVars.BarksEnabled) &&
            TryComp<SpeechBarksComponent>(speaker, out var barks) &&
            _proto.HasIndex<BarkPrototype>(barks.Data.Proto);
    }

    public uint TrackSpeechBubble(EntityUid speaker, string message, Action<float> reveal)
    {
        if (_speechBubbleReveals.TryGetValue(speaker, out var previous))
            previous.Update(1f);

        var registrationId = ++_nextSpeechBubbleRevealId;
        _speechBubbleReveals[speaker] = new SpeechBubbleReveal(registrationId, message, reveal);

        for (var i = _activeBarks.Count - 1; i >= 0; i--)
        {
            var bark = _activeBarks[i];
            if (bark.Source != speaker || bark.IsRadio)
                continue;

            bark.Reveal = null;
            if (bark.Message != message)
                continue;

            bark.Reveal = reveal;
            reveal(bark.StepIndex == 0 ? 0f : bark.Steps[bark.StepIndex - 1].RevealProgress);
            break;
        }

        return registrationId;
    }

    public void UntrackSpeechBubble(EntityUid speaker, uint registrationId)
    {
        if (!_speechBubbleReveals.TryGetValue(speaker, out var tracked) || tracked.Id != registrationId)
            return;

        _speechBubbleReveals.Remove(speaker);

        foreach (var bark in _activeBarks)
            if (bark.Source == speaker)
                bark.Reveal = null;
    }

    private sealed class ActiveBark
    {
        public readonly EntityUid? Source;
        public readonly NetEntity? Speaker;
        public readonly string Message;
        public readonly SoundSpecifier Sound = default!;
        public readonly float Volume = default!;
        public readonly float Pitch = default!;
        public readonly float Distance = default!;
        public readonly (float, float) DelayVariation = default!;
        public readonly List<SpeechStep> Steps;
        public readonly float TotalDelayWeight;
        public readonly bool HasSource;
        public readonly bool IsRadio;
        public readonly bool IsShouting;

        public TimeSpan NextSound = TimeSpan.Zero;
        public int StepIndex;
        public Action<float>? Reveal;

        public ActiveBark(EntityUid? source, NetEntity? speaker, SoundSpecifier sound, float volume, float pitch, float distance, (float, float) delay, List<SpeechStep> steps, bool isRadio, string message = "", bool isShouting = false)
        {
            Source = source;
            Speaker = speaker;
            Message = message;
            HasSource = source.HasValue;
            IsRadio = isRadio;
            IsShouting = isShouting;
            Sound = sound;
            Volume = volume;
            Pitch = pitch;
            Distance = distance;
            DelayVariation = delay;
            Steps = steps;

            foreach (var step in steps)
                TotalDelayWeight += step.DelayWeight;
        }
    }

    private readonly struct SpeechStep(bool playSound, float revealProgress, float delayWeight, SpeechPause pause)
    {
        public readonly bool PlaySound = playSound;
        public readonly float RevealProgress = revealProgress;
        public readonly float DelayWeight = delayWeight;
        public readonly SpeechPause Pause = pause;
    }

    private enum SpeechPause : byte
    {
        None,
        Comma,
        Sentence,
        Ellipsis,
    }

    private sealed class SpeechBubbleReveal(uint id, string message, Action<float> update)
    {
        public readonly uint Id = id;
        public readonly string Message = message;
        public readonly Action<float> Update = update;
    }
}

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared._Onyx.StationEvents;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.StationEvents.SecretPlus;

public sealed class SelectedEvent
{
    public readonly EntityPrototype Proto;
    public readonly GameRuleComponent Rule;
    public readonly StationEventComponent Event;

    public SelectedEvent(EntityPrototype proto, GameRuleComponent rule, StationEventComponent stationEvent)
    {
        Proto = proto;
        Rule = rule;
        Event = stationEvent;
    }
}

public sealed class PlayerCount
{
    public int Players;
    public int Ghosts;
}

[UsedImplicitly]
public sealed partial class SecretPlusSystem : GameRuleSystem<SecretPlusComponent>
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private EventManagerSystem _event = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private TagSystem _tag = default!;

    private float _eventSpeedup;
    private int _playerCountBias;
    private float _minimumTimeUntilFirstEvent;
    private float _roundstartChaosScoreMultiplier;
    private static readonly ProtoId<TagPrototype> LoneSpawnTag = "LoneRunRule";

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.StationEventSpeedup, value => _eventSpeedup = Math.Max(value, 0.01f), true);
        Subs.CVar(_cfg, CCVars.StationEventPlayerBias, value => _playerCountBias = value, true);
        Subs.CVar(_cfg, CCVars.MinimumTimeUntilFirstEvent, value => _minimumTimeUntilFirstEvent = Math.Max(value, 0f), true);
        Subs.CVar(_cfg, CCVars.RoundstartChaosScoreMultiplier, value => _roundstartChaosScoreMultiplier = Math.Max(value, 0f), true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        ValidatePrototypes();
    }

    protected override void Added(EntityUid uid, SecretPlusComponent scheduler, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        var totalPlayers = GetTotalPlayerCount();
        var minimumStartingChaos = Math.Max(0f, Math.Min(scheduler.MinStartingChaos, scheduler.MaxStartingChaos));
        var maximumStartingChaos = Math.Max(minimumStartingChaos, Math.Max(scheduler.MinStartingChaos, scheduler.MaxStartingChaos));
        SetChaos(scheduler, -_random.NextFloat(
            minimumStartingChaos * totalPlayers,
            maximumStartingChaos * totalPlayers) * _roundstartChaosScoreMultiplier);

        var roll = MathF.Pow(_random.NextFloat(), Math.Max(0f, scheduler.ChaosChangeVariationExponent));
        var minimumVariation = Math.Max(0f, scheduler.ChaosChangeVariationMin);
        var maximumVariation = Math.Max(minimumVariation, scheduler.ChaosChangeVariationMax);
        scheduler.ChaosChangeVariation = MathHelper.Lerp(
            1f,
            _random.Prob(0.5f) ? minimumVariation : maximumVariation,
            roll);

        LogMessage($"Using chaos change multiplier of {scheduler.ChaosChangeVariation}");
        TrySpawnRoundstartAntags((uid, scheduler));
        SetupEvents((uid, scheduler), CountActivePlayers(scheduler));
    }

    protected override void ActiveTick(EntityUid uid, SecretPlusComponent scheduler, GameRuleComponent gameRule, float frameTime)
    {
        if (!_event.EventsEnabled)
            return;

        var count = CountActivePlayers(scheduler);
        if (count.Players < Math.Max(0, scheduler.MinimumActivePlayers))
        {
            if (scheduler.TimeNextEvent != TimeSpan.Zero)
                scheduler.TimeNextEvent += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var ramp = GetRamping((uid, scheduler));
        var multiplier = scheduler.ChaosChangeVariation;

        var chaosChange = count.Players * scheduler.LivingChaosChange * frameTime * ramp * _eventSpeedup * multiplier;
        chaosChange += count.Ghosts * scheduler.DeadChaosChange * frameTime * _eventSpeedup * multiplier;
        SetChaos(scheduler, scheduler.ChaosScore + chaosChange);

        var currentTime = _timing.CurTime;
        if (currentTime < scheduler.TimeNextEvent)
            return;

        if (scheduler.TimeNextEvent == TimeSpan.Zero)
        {
            var delay = _minimumTimeUntilFirstEvent / _eventSpeedup;
            scheduler.TimeNextEvent = currentTime + TimeSpan.FromSeconds(delay);
            LogMessage($"Started, first event in {delay} seconds");
            return;
        }

        var minimumInterval = Math.Max(scheduler.MinimumEventInterval.TotalSeconds,
            Math.Min(scheduler.EventIntervalMin.TotalSeconds, scheduler.EventIntervalMax.TotalSeconds));
        var maximumInterval = Math.Max(minimumInterval,
            Math.Max(scheduler.EventIntervalMin.TotalSeconds, scheduler.EventIntervalMax.TotalSeconds));
        var delaySeconds = Math.Max(scheduler.MinimumEventInterval.TotalSeconds,
            _random.NextDouble(minimumInterval, maximumInterval) / ramp / _eventSpeedup);
        var delayTime = TimeSpan.FromSeconds(delaySeconds);
        scheduler.TimeNextEvent = currentTime + delayTime;
        LogMessage($"Chaos score: {scheduler.ChaosScore}, Next event at: {GameTicker.RoundDuration() + delayTime} (ramping {ramp})");

        SetupEvents((uid, scheduler), count);
        if (MathF.Abs(scheduler.ChaosScore) <= Math.Max(0f, scheduler.ChaosDeadZone))
            LogMessage("Chaos is inside the dead zone");
        else if (ChooseEvent((uid, scheduler)) is { } selected)
            StartRule((uid, scheduler), selected.Proto.ID);
        else
            LogMessage("No runnable events");
    }

    private void SetupEvents(Entity<SecretPlusComponent> scheduler, PlayerCount count)
    {
        scheduler.Comp.SelectedEvents.Clear();

        IEnumerable<EntityPrototype> prototypes;
        if (scheduler.Comp.ScheduledGameRules is { } table)
        {
            prototypes = _entityTable.GetSpawns(table)
                .Select(id => _prototypeManager.TryIndex(id, out EntityPrototype? proto) ? proto : null)
                .OfType<EntityPrototype>();
        }
        else
        {
            prototypes = _event.AllEvents().Keys;
        }

        foreach (var proto in prototypes)
        {
            if (!proto.TryComp<GameRuleComponent>(out var rule, _factory)
                || !proto.TryComp<StationEventComponent>(out var stationEvent, _factory)
                || IsDisallowed(scheduler.Comp, proto)
                || (!scheduler.Comp.IgnoreTimings && !CanRun(proto, stationEvent, count.Players, 1f / GetRamping(scheduler))))
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, rule, stationEvent));
        }
    }

    private bool IsDisallowed(SecretPlusComponent scheduler, EntityPrototype proto)
    {
        if (proto.TryComp<SecretPlusEventComponent>(out var component, _factory))
            return scheduler.DisallowedEvents.Contains(component.EventType);

        return TryGetMetadata(proto.ID, out var metadata)
            && metadata.EventType is { } eventType
            && scheduler.DisallowedEvents.Contains(eventType);
    }

    private bool CanRun(EntityPrototype proto, StationEventComponent stationEvent, int players, float recurrenceMultiplier)
    {
        if (GameTicker.IsGameRuleActive(proto.ID))
            return false;

        if (stationEvent.MaxOccurrences is { } max
            && GameTicker.AllPreviousGameRules.Count(entry => entry.Item2 == proto.ID) >= max)
            return false;

        if (players < stationEvent.MinimumPlayers)
            return false;

        var roundTime = GameTicker.RoundDuration();
        if (roundTime != TimeSpan.Zero && roundTime.TotalMinutes < stationEvent.EarliestStart / _eventSpeedup)
            return false;

        var lastRun = _event.TimeSinceLastEvent(proto);
        if (lastRun != TimeSpan.Zero
            && roundTime.TotalMinutes < stationEvent.ReoccurrenceDelay * recurrenceMultiplier / _eventSpeedup + lastRun.TotalMinutes)
            return false;

        return !_roundEnd.IsRoundEndRequested() || stationEvent.OccursDuringRoundEnd || _roundEnd.CanCallOrRecall();
    }

    private void TrySpawnRoundstartAntags(Entity<SecretPlusComponent> scheduler)
    {
        if (scheduler.Comp.NoRoundstartAntags)
            return;

        if (!_prototypeManager.TryIndex(scheduler.Comp.PrimaryAntagsWeightTable, out var primaryTable)
            || !_prototypeManager.TryIndex(scheduler.Comp.RoundStartAntagsWeightTable, out var table))
        {
            Log.Error("SecretPlus roundstart weight table is missing");
            return;
        }

        var primaryWeights = primaryTable.Weights.Where(entry => entry.Value > 0f && float.IsFinite(entry.Value)).ToDictionary();
        var weights = table.Weights.Where(entry => entry.Value > 0f && float.IsFinite(entry.Value)).ToDictionary();
        var playerCount = GetTotalPlayerCount();
        var originalChaos = scheduler.Comp.ChaosScore;

        if (primaryWeights.Count == 0 || originalChaos >= 0f)
            return;

        LogMessage($"Trying to run roundstart rules, total player count: {playerCount}", false);

        var rulesStarted = 0;
        for (var iteration = 1;
             scheduler.Comp.ChaosScore < 0 && iteration <= 50 && rulesStarted < Math.Max(0, scheduler.Comp.MaximumRoundstartRules);
             iteration++)
        {
            var picks = iteration == 1 ? primaryWeights : weights;
            if (picks.Count == 0)
                return;

            var pick = _random.Pick(picks);
            if (!_prototypeManager.TryIndex(pick, out EntityPrototype? proto)
                || !proto.TryComp<GameRuleComponent>(out var rule, _factory))
            {
                picks.Remove(pick);
                continue;
            }

            var chaos = GetChaosScore(proto, rule, playerCount);
            if (chaos is null or <= 0f || !float.IsFinite(chaos.Value))
            {
                Log.Error($"Tried running roundstart event {proto.ID}, but chaos score was invalid");
                picks.Remove(pick);
                continue;
            }

            var probability = Math.Clamp(-scheduler.Comp.ChaosScore / chaos.Value
                * (iteration == 1 ? Math.Max(0f, scheduler.Comp.PrimaryAntagChaosBias) : 1f), 0f, 1f);
            if (!_random.Prob(probability))
                continue;

            if (!scheduler.Comp.IgnoreIncompatible)
            {
                weights.Remove(pick);
                if (_prototypeManager.TryIndex(pick, out IncompatibleGameModesPrototype? incompatible))
                    weights = weights.Where(entry => !incompatible.Modes.Contains(entry.Key)).ToDictionary();
            }

            if (rule.MinPlayers <= playerCount)
            {
                var effectivePlayers = (int) MathF.Round(playerCount * scheduler.Comp.ChaosScore / originalChaos);
                var effectiveChaos = GetChaosScore(proto, rule, effectivePlayers);
                if (effectiveChaos == null || !CanAfford(scheduler.Comp, effectiveChaos.Value))
                {
                    weights.Remove(pick);
                    continue;
                }

                LogMessage($"Roundstart rule chosen: {pick} with score {effectiveChaos}");
                StartRule(scheduler, pick, false, effectivePlayers);
                rulesStarted++;
            }

            if (weights.Count == 0
                || (!scheduler.Comp.IgnoreIncompatible && IsLoneRule(proto)))
                return;
        }
    }

    private void StartRule(Entity<SecretPlusComponent> scheduler, string rule, bool start = true, int? players = null)
    {
        var ruleUid = GameTicker.AddGameRule(rule);
        var chaos = GetChaosScore(ruleUid, players);
        if (chaos == null)
        {
            Log.Error($"Tried running rule {rule}, but chaos score was null");
            QueueDel(ruleUid);
            return;
        }

        if (!float.IsFinite(chaos.Value) || !CanAfford(scheduler.Comp, chaos.Value))
        {
            Log.Error($"Tried running unaffordable or invalid rule {rule} with chaos score {chaos}");
            QueueDel(ruleUid);
            return;
        }

        SetChaos(scheduler.Comp, scheduler.Comp.ChaosScore + chaos.Value);

        if (players != null && TryComp<AntagSelectionComponent>(ruleUid, out var selection))
        {
            var runningCount = 0;
            for (var i = 0; i < selection.Antags.Length; i++)
            {
                var selector = selection.Antags[i];
                var count = _antagSelection.GetTargetAntagCount(selector, players.Value, ref runningCount);
                selection.Antags[i] = new SecretPlusFixedAntagCount
                {
                    Proto = selector.Proto,
                    PlayerRatio = selector.PlayerRatio,
                    Count = count,
                };
            }
        }

        if (start)
            GameTicker.StartGameRule(ruleUid);
    }

    public float? GetChaosScore(Entity<GameRuleComponent?> rule, int? players = null)
    {
        if (!Resolve(rule, ref rule.Comp))
            return null;

        if (TryComp<SecretPlusChaosComponent>(rule, out var chaos))
            return GetChaosScore((rule.Owner, chaos), players ?? GetTotalPlayerCount());

        var id = MetaData(rule.Owner).EntityPrototype?.ID;
        return id != null && TryGetMetadata(id, out var metadata)
            ? GetChaosScore(rule.Owner, metadata, players ?? GetTotalPlayerCount())
            : null;
    }

    public float? GetChaosScore(EntityPrototype proto, GameRuleComponent? rule = null, int? players = null)
    {
        if (rule == null && !proto.TryComp<GameRuleComponent>(out rule, _factory))
            return null;

        if (proto.TryComp<SecretPlusChaosComponent>(out var chaos, _factory))
            return GetChaosScore(proto, chaos, players ?? GetTotalPlayerCount());

        return TryGetMetadata(proto.ID, out var metadata)
            ? GetChaosScore(proto, metadata, players ?? GetTotalPlayerCount())
            : null;
    }

    private float? GetChaosScore(Entity<SecretPlusChaosComponent> rule, int players)
    {
        if (!TryComp<AntagSelectionComponent>(rule, out var selection) || rule.Comp.AntagChaosScores.Count == 0)
            return rule.Comp.ChaosScore;

        var any = false;
        var score = 0f;
        var runningCount = 0;
        foreach (var selector in selection.Antags)
        {
            var count = _antagSelection.GetTargetAntagCount(selector, players, ref runningCount);
            if (!rule.Comp.AntagChaosScores.TryGetValue(selector.Proto, out var antagChaos))
                continue;

            any = true;
            score += antagChaos * count;
        }

        return any ? score : rule.Comp.ChaosScore;
    }

    private float? GetChaosScore(EntityPrototype proto, SecretPlusChaosComponent chaos, int players)
    {
        if (!proto.TryComp<AntagSelectionComponent>(out var selection, _factory) || chaos.AntagChaosScores.Count == 0)
            return chaos.ChaosScore;

        var any = false;
        var score = 0f;
        var runningCount = 0;
        foreach (var selector in selection.Antags)
        {
            var count = _antagSelection.GetTargetAntagCount(selector, players, ref runningCount);
            if (!chaos.AntagChaosScores.TryGetValue(selector.Proto, out var antagChaos))
                continue;

            any = true;
            score += antagChaos * count;
        }

        return any ? score : chaos.ChaosScore;
    }

    private float? GetChaosScore(EntityUid rule, SecretPlusRulePrototype metadata, int players)
    {
        return TryComp<AntagSelectionComponent>(rule, out var selection)
            ? GetMetadataChaos(selection, metadata, players)
            : metadata.ChaosScore;
    }

    private float? GetChaosScore(EntityPrototype rule, SecretPlusRulePrototype metadata, int players)
    {
        return rule.TryComp<AntagSelectionComponent>(out var selection, _factory)
            ? GetMetadataChaos(selection, metadata, players)
            : metadata.ChaosScore;
    }

    private float? GetMetadataChaos(AntagSelectionComponent selection, SecretPlusRulePrototype metadata, int players)
    {
        if (metadata.AntagChaosScores.Count == 0)
            return metadata.ChaosScore;

        var any = false;
        var score = 0f;
        var runningCount = 0;
        foreach (var selector in selection.Antags)
        {
            var count = _antagSelection.GetTargetAntagCount(selector, players, ref runningCount);
            if (!metadata.AntagChaosScores.TryGetValue(selector.Proto, out var antagChaos))
                continue;

            any = true;
            score += antagChaos * count;
        }

        return any ? score : metadata.ChaosScore;
    }

    private bool TryGetMetadata(string rule, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SecretPlusRulePrototype? metadata)
    {
        return _prototypeManager.TryIndex<SecretPlusRulePrototype>(rule, out metadata);
    }

    private bool IsLoneRule(EntityPrototype proto)
    {
        if (TryGetMetadata(proto.ID, out var metadata) && metadata.LoneRule)
            return true;

        return proto.TryComp<TagComponent>(out var tags, _factory) && _tag.HasTag(tags, LoneSpawnTag);
    }

    public int GetTotalPlayerCount()
    {
        return Math.Max(0, _playerManager.Sessions.Count(session =>
            session.Status is not (SessionStatus.Disconnected or SessionStatus.Zombie)) + _playerCountBias);
    }

    public float GetRamping(Entity<SecretPlusComponent> scheduler)
    {
        return Math.Clamp(
            1f + (float) GameTicker.RoundDuration().TotalSeconds * Math.Max(0f, scheduler.Comp.SpeedRamping) * _eventSpeedup,
            1f,
            Math.Max(1f, scheduler.Comp.MaximumRamping));
    }

    private PlayerCount CountActivePlayers(SecretPlusComponent scheduler)
    {
        var count = new PlayerCount();
        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { } attached)
                continue;

            if (HasComp<HumanoidProfileComponent>(attached))
                count.Players++;
            else if (TryComp<GhostComponent>(attached, out var ghost) && ghost.CanReturnToBody)
                count.Ghosts++;
        }

        count.Players = Math.Max(0, count.Players + _playerCountBias);
        count.Ghosts = Math.Min(count.Ghosts, Math.Max(0, scheduler.MaximumGhostContribution));
        return count;
    }

    private SelectedEvent? ChooseEvent(Entity<SecretPlusComponent> scheduler)
    {
        var weights = new Dictionary<SelectedEvent, float>();
        foreach (var selected in scheduler.Comp.SelectedEvents)
        {
            var chaos = GetChaosScore(selected.Proto, selected.Rule);
            if (chaos == null)
            {
                Log.Error($"Tried running event {selected.Proto.ID}, but chaos score was null");
                continue;
            }

            if (!float.IsFinite(chaos.Value))
                continue;

            if (!CanAfford(scheduler.Comp, chaos.Value))
                continue;

            var exponent = Math.Max(0f, scheduler.Comp.ChaosExponent);
            var threshold = Math.Max(0.01f, scheduler.Comp.ChaosThreshold);
            var matching = Math.Max(1.01f, scheduler.Comp.ChaosMatching);
            var weight = MathF.Pow(MathF.Abs(chaos.Value), exponent) * MathF.Sign(chaos.Value);
            weight += scheduler.Comp.ChaosOffset;
            weight += weight < 0f ? -threshold : threshold;
            var delta = ChaosDelta(
                -scheduler.Comp.ChaosScore,
                weight,
                matching,
                threshold * threshold,
                threshold);
            var selectionWeight = selected.Event.Weight / (delta + 1f);
            if (selectionWeight > 0f && float.IsFinite(selectionWeight))
                weights[selected] = selectionWeight;
        }

        return weights.Count == 0 ? null : _random.Pick(weights);
    }

    public static bool CanAfford(SecretPlusComponent scheduler, float cost)
    {
        return float.IsFinite(cost) && (cost <= 0f || cost <= Math.Max(0f, -scheduler.ChaosScore));
    }

    public static float ChaosDelta(float chaos1, float chaos2, float logBase, float differentSignMultiplier, float minimumMagnitude)
    {
        chaos1 = MathF.CopySign(Math.Max(MathF.Abs(chaos1), minimumMagnitude), chaos1 == 0f ? 1f : chaos1);
        chaos2 = MathF.CopySign(Math.Max(MathF.Abs(chaos2), minimumMagnitude), chaos2 == 0f ? 1f : chaos2);
        var ratio = chaos2 / chaos1;
        if (ratio < 0f)
            ratio = MathF.Abs(chaos2 * chaos1 / differentSignMultiplier);
        return MathF.Abs(MathF.Log(ratio, logBase));
    }

    private static void SetChaos(SecretPlusComponent scheduler, float value)
    {
        var limit = Math.Max(1f, scheduler.MaximumAbsoluteChaos);
        scheduler.ChaosScore = float.IsFinite(value) ? Math.Clamp(value, -limit, limit) : 0f;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>()
            || args.WasModified<WeightedRandomPrototype>()
            || args.WasModified<EventTypePrototype>()
            || args.WasModified<SecretPlusRulePrototype>()
            || args.WasModified<IncompatibleGameModesPrototype>())
            ValidatePrototypes();
    }

    private void ValidatePrototypes()
    {
        foreach (var metadata in _prototypeManager.EnumeratePrototypes<SecretPlusRulePrototype>())
            ValidateMetadata(metadata);

        foreach (var incompatible in _prototypeManager.EnumeratePrototypes<IncompatibleGameModesPrototype>())
        {
            foreach (var mode in incompatible.Modes)
            {
                if (!_prototypeManager.HasIndex<EntityPrototype>(mode))
                    Log.Error($"SecretPlus incompatible modes {incompatible.ID} references missing rule {mode}");
            }
        }

        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (proto.TryComp<SecretPlusComponent>(out var scheduler, _factory))
                ValidateScheduler(proto, scheduler);

            if (proto.TryComp<SecretPlusEventComponent>(out var eventMetadata, _factory)
                && !_prototypeManager.HasIndex<EventTypePrototype>(eventMetadata.EventType))
                Log.Error($"SecretPlus event {proto.ID} has no event type {eventMetadata.EventType}");

            if (!proto.TryComp<SecretPlusChaosComponent>(out var chaos, _factory))
                continue;

            if (chaos.ChaosScore is { } score && !float.IsFinite(score))
                Log.Error($"SecretPlus rule {proto.ID} has a non-finite chaos score");

            foreach (var (antag, antagScore) in chaos.AntagChaosScores)
            {
                if (!float.IsFinite(antagScore))
                    Log.Error($"SecretPlus rule {proto.ID} has a non-finite chaos score for antag {antag}");
            }

            if (!proto.TryComp<AntagSelectionComponent>(out var selection, _factory)
                || chaos.AntagChaosScores.Count == 0)
                continue;

            foreach (var selector in selection.Antags)
            {
                if (!chaos.AntagChaosScores.ContainsKey(selector.Proto))
                    Log.Error($"SecretPlus rule {proto.ID} has no chaos score for antag {selector.Proto}");
            }
        }
    }

    private void ValidateMetadata(SecretPlusRulePrototype metadata)
    {
        if (!_prototypeManager.TryIndex(metadata.Rule, out EntityPrototype? rule))
        {
            Log.Error($"SecretPlus metadata {metadata.ID} references missing rule {metadata.Rule}");
            return;
        }

        if (metadata.EventType is { } eventType && !_prototypeManager.HasIndex<EventTypePrototype>(eventType))
            Log.Error($"SecretPlus metadata {metadata.ID} references missing event type {eventType}");

        if (metadata.ChaosScore is { } score && !float.IsFinite(score))
            Log.Error($"SecretPlus metadata {metadata.ID} has a non-finite chaos score");

        foreach (var (antag, antagScore) in metadata.AntagChaosScores)
        {
            if (!float.IsFinite(antagScore))
                Log.Error($"SecretPlus metadata {metadata.ID} has a non-finite chaos score for antag {antag}");
        }

        if (!rule.TryComp<AntagSelectionComponent>(out var selection, _factory)
            || metadata.AntagChaosScores.Count == 0)
            return;

        foreach (var selector in selection.Antags)
        {
            if (!metadata.AntagChaosScores.ContainsKey(selector.Proto))
                Log.Error($"SecretPlus metadata {metadata.ID} has no chaos score for antag {selector.Proto}");
        }
    }

    private void ValidateScheduler(EntityPrototype proto, SecretPlusComponent scheduler)
    {
        foreach (var eventType in scheduler.DisallowedEvents)
        {
            if (!_prototypeManager.HasIndex<EventTypePrototype>(eventType))
                Log.Error($"SecretPlus scheduler {proto.ID} references missing event type {eventType}");
        }

        if (scheduler.EventIntervalMin < TimeSpan.Zero
            || scheduler.EventIntervalMax < TimeSpan.Zero
            || scheduler.MinimumEventInterval < TimeSpan.Zero)
            Log.Error($"SecretPlus scheduler {proto.ID} has a negative event interval");

        if (scheduler.MaximumAbsoluteChaos <= 0f
            || scheduler.MaximumRamping < 1f
            || scheduler.ChaosMatching <= 1f
            || scheduler.ChaosThreshold <= 0f
            || scheduler.ChaosDeadZone < 0f
            || !float.IsFinite(scheduler.MaximumAbsoluteChaos)
            || !float.IsFinite(scheduler.MaximumRamping)
            || !float.IsFinite(scheduler.ChaosMatching)
            || !float.IsFinite(scheduler.ChaosThreshold)
            || !float.IsFinite(scheduler.ChaosDeadZone))
            Log.Error($"SecretPlus scheduler {proto.ID} has invalid chaos limits");

        if (scheduler.MinimumActivePlayers < 0
            || scheduler.MaximumGhostContribution < 0
            || scheduler.MaximumRoundstartRules < 0)
            Log.Error($"SecretPlus scheduler {proto.ID} has invalid count limits");

        if (!_prototypeManager.TryIndex(scheduler.PrimaryAntagsWeightTable, out var primaryTable))
            Log.Error($"SecretPlus scheduler {proto.ID} has no primary antag table {scheduler.PrimaryAntagsWeightTable}");
        else if (primaryTable.Weights.Count == 0 || primaryTable.Weights.Any(entry => entry.Value <= 0f || !float.IsFinite(entry.Value)))
            Log.Error($"SecretPlus scheduler {proto.ID} has invalid weights in {scheduler.PrimaryAntagsWeightTable}");

        if (!_prototypeManager.TryIndex(scheduler.RoundStartAntagsWeightTable, out var roundstartTable))
            Log.Error($"SecretPlus scheduler {proto.ID} has no roundstart antag table {scheduler.RoundStartAntagsWeightTable}");
        else if (roundstartTable.Weights.Count == 0 || roundstartTable.Weights.Any(entry => entry.Value <= 0f || !float.IsFinite(entry.Value)))
            Log.Error($"SecretPlus scheduler {proto.ID} has invalid weights in {scheduler.RoundStartAntagsWeightTable}");
    }

    public IEnumerable<string> GetStatus()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var scheduler, out _))
        {
            var count = CountActivePlayers(scheduler);
            var ramp = GetRamping((uid, scheduler));
            var rate = (count.Players * scheduler.LivingChaosChange + count.Ghosts * scheduler.DeadChaosChange)
                * ramp * _eventSpeedup * scheduler.ChaosChangeVariation;
            yield return $"{ToPrettyString(uid)}: chaos={scheduler.ChaosScore:F2}, budget={Math.Max(0f, -scheduler.ChaosScore):F2}, rate={rate:F3}/s, ramp={ramp:F2}, players={count.Players}, ghosts={count.Ghosts}, next={scheduler.TimeNextEvent - _timing.CurTime}";

            foreach (var candidate in scheduler.SelectedEvents
                         .Select(selected => (selected.Proto.ID, Cost: GetChaosScore(selected.Proto, selected.Rule)))
                         .Where(entry => entry.Cost != null && CanAfford(scheduler, entry.Cost.Value))
                         .OrderBy(entry => MathF.Abs(entry.Cost!.Value + scheduler.ChaosScore))
                         .Select(entry => (entry.ID, Cost: entry.Cost!.Value))
                         .Take(5))
                yield return $"  candidate={candidate.ID}, cost={candidate.Cost:F2}";
        }
    }

    private void LogMessage(string message, bool showChat = true)
    {
        _adminLogger.Add(LogType.EventRan, showChat ? LogImpact.Medium : LogImpact.High, $"{message}");
        if (showChat)
            _chat.SendAdminAnnouncement("SecretPlus " + message);
    }
}

public sealed partial class SecretPlusFixedAntagCount : AntagCountSelector
{
    public int Count;

    public override int GetTargetAntagCount(IRobustRandom random, int playerCount)
    {
        return Count;
    }
}

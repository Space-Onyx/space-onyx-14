// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._Onyx.Mood;
using Content.Shared._Onyx.Overlays;
using Content.Shared._Onyx.Roles;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Onyx.Mood;

/// <summary>
///     Simulates crew mood: collects moodlets from the world, folds them into
///     a level and sanity value, and fans the result out to speed, crit
///     thresholds, alerts and client visuals.
/// </summary>
public sealed partial class MoodSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _protos = default!;

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedJetpackSystem _jetpack = default!;
    [Dependency] private AtmosphereSystem _atmo = default!;
    [Dependency] private BarotraumaSystem _barotrauma = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    private const float SanityTick = 1f;
    private float _sanityAccumulator;

    private static readonly Dictionary<SatiationValue, ProtoId<MoodEffectPrototype>> HungerMoodlets = new()
    {
        ["Overfed"] = "HungerOverfed",
        ["Okay"] = "HungerOkay",
        ["Peckish"] = "HungerPeckish",
        ["Starving"] = "HungerStarving",
    };

    private static readonly Dictionary<SatiationValue, ProtoId<MoodEffectPrototype>> ThirstMoodlets = new()
    {
        ["Overhydrated"] = "ThirstOverHydrated",
        ["Okay"] = "ThirstOkay",
        ["Thirsty"] = "ThirstThirsty",
        ["Parched"] = "ThirstParched",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodComponent, ComponentStartup>(OnComponentInit);
        SubscribeLocalEvent<MoodComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<MoodComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MoodComponent, MoodEffectEvent>(OnMoodEffectApplied);
        SubscribeLocalEvent<MoodComponent, MoodRemoveEffectEvent>(OnMoodEffectLifted);
        SubscribeLocalEvent<MoodComponent, MoodPurgeEffectsEvent>(OnMoodEffectsPurged);
        SubscribeLocalEvent<MoodComponent, ShowMoodAlertEvent>(OnMoodAlertShown);
        SubscribeLocalEvent<MoodComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
        SubscribeLocalEvent<MoodComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MoodComponent, CuffedStateChangeEvent>(OnCuffedChanged);
        SubscribeLocalEvent<MoodComponent, SuffocationEvent>(OnSuffocationStarted);
        SubscribeLocalEvent<MoodComponent, StopSuffocatingEvent>(OnSuffocationStopped);
        SubscribeLocalEvent<MoodComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<MoodComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<MoodComponent, SatiationUpdateEvent>(OnSatiationUpdated);
        SubscribeLocalEvent<MoodComponent, MoodVomitEvent>(OnVomitRelayed);
        SubscribeLocalEvent<MoodComponent, MoodCreamPiedEvent>(OnCreamPiedRelayed);
        SubscribeLocalEvent<SlipEvent>(OnSlipped);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_config.GetCVar(CCVars.MoodEnabled))
            return;

        _sanityAccumulator += frameTime;
        if (_sanityAccumulator < SanityTick)
            return;

        var elapsed = _sanityAccumulator;
        _sanityAccumulator = 0f;

        var query = EntityQueryEnumerator<MoodComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var mood, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            TickSanity(uid, mood, elapsed);
            TickPressure(uid);
        }
    }

    /// <summary>
    ///     Public entry point for other systems: queue a moodlet application.
    /// </summary>
    public void AddEffect(EntityUid uid, string effectId, float modifier = 1f, float offset = 0f)
    {
        RaiseLocalEvent(uid, new MoodEffectEvent(effectId, modifier, offset));
    }

    /// <summary>
    ///     Public entry point for other systems: queue a moodlet removal.
    /// </summary>
    public void RemoveEffect(EntityUid uid, string effectId, MoodEffectRemovalReason reason = MoodEffectRemovalReason.Manual)
    {
        RaiseLocalEvent(uid, new MoodRemoveEffectEvent(effectId, reason));
    }

    private void OnComponentInit(EntityUid uid, MoodComponent component, ComponentStartup args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled))
            return;

        if (_config.GetCVar(CCVars.MoodModifiesThresholds)
            && TryComp<MobThresholdsComponent>(uid, out var mobThresholds))
            CacheBaselineThresholds(uid, component, mobThresholds);

        EnsureComp<NetMoodComponent>(uid);
        SyncSatiationMoodlet(uid, SatiationSystem.Hunger);
        SyncSatiationMoodlet(uid, SatiationSystem.Thirst);
        Recalculate(uid, component);
        ApplyThresholdBand(uid, component, force: true);
        ShiftCritThreshold(uid, component, SpeedGroupOf(component.CurrentMoodThreshold));
    }

    private void OnComponentShutdown(EntityUid uid, MoodComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(uid, component.MoodCategory);
        RemComp<SaturationScaleOverlayComponent>(uid);
        RestoreBaselineThresholds(uid, component);
    }

    private void OnMoodEffectApplied(EntityUid uid, MoodComponent component, MoodEffectEvent args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled)
            || !_protos.TryIndex<MoodEffectPrototype>(args.EffectId, out var prototype))
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState)
            && mobState.CurrentState == MobState.Dead
            && args.EffectId != "Dead")
            return;

        var tweak = new OnMoodEffect(uid, args.EffectId, args.EffectModifier, args.EffectOffset);
        RaiseLocalEvent(uid, ref tweak);

        StoreMoodlet(uid, component, prototype, tweak.EffectModifier, tweak.EffectOffset);
    }

    private void OnMoodEffectLifted(EntityUid uid, MoodComponent component, MoodRemoveEffectEvent args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled))
            return;

        if (component.UncategorisedEffects.ContainsKey(args.EffectId))
        {
            DropMoodlet(uid, args.EffectId, null, args.Reason);
            return;
        }

        foreach (var (category, id) in component.CategorisedEffects)
        {
            if (id != args.EffectId)
                continue;

            DropMoodlet(uid, args.EffectId, category, args.Reason);
            return;
        }
    }

    private void OnMoodEffectsPurged(EntityUid uid, MoodComponent component, MoodPurgeEffectsEvent args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled))
            return;

        var condemned = new List<string>();
        foreach (var (id, _) in component.UncategorisedEffects)
        {
            if (!_protos.TryIndex(id, out MoodEffectPrototype? proto)
                || proto.Timeout == 0 && !args.RemovePermanentMoodlets)
                continue;

            condemned.Add(id);
        }

        foreach (var id in condemned)
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent(id));
    }

    /// <summary>
    ///     Inserts a moodlet into the right bucket and refreshes derived totals.
    /// </summary>
    private void StoreMoodlet(EntityUid uid, MoodComponent component, MoodEffectPrototype prototype, float modifier = 1f, float offset = 0f)
    {
        if (prototype.Category != null)
        {
            if (component.CategorisedEffects.TryGetValue(prototype.Category, out var currentId))
            {
                if (!_protos.TryIndex<MoodEffectPrototype>(currentId, out var current))
                    return;

                if (!component.CategorisedEffects.ContainsValue(prototype.ID))
                    AnnounceMoodlet(uid, prototype);

                if (prototype.ID != current.ID)
                    component.CategorisedEffects[prototype.Category] = prototype.ID;
            }
            else
            {
                component.CategorisedEffects.Add(prototype.Category, prototype.ID);
                AnnounceMoodlet(uid, prototype);
            }
        }
        else
        {
            if (component.UncategorisedEffects.ContainsKey(prototype.ID))
                return;

            var applied = prototype.MoodChange * modifier + offset;
            if (applied == 0)
                return;

            AnnounceMoodlet(uid, prototype);
            component.UncategorisedEffects.Add(prototype.ID, applied);
        }

        ArmExpiry(uid, component, prototype);
        Recalculate(uid, component);
    }

    private void AnnounceMoodlet(EntityUid uid, MoodEffectPrototype prototype)
    {
        if (prototype.Hidden)
            return;

        _popup.PopupEntity(prototype.Description(uid),
            uid,
            uid,
            prototype.MoodChange > 0 ? PopupType.Medium : PopupType.MediumCaution);
    }

    /// <summary>
    ///     Schedules timed removal, bumping a generation so a refreshed moodlet
    ///     does not get killed by its own previous timer.
    /// </summary>
    private void ArmExpiry(EntityUid uid, MoodComponent component, MoodEffectPrototype prototype)
    {
        if (prototype.Timeout == 0)
            return;

        if (prototype.Category != null)
        {
            component.CategorisedEffectTimerGenerations.TryGetValue(prototype.Category, out var generation);
            generation += 1;
            component.CategorisedEffectTimerGenerations[prototype.Category] = generation;

            Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout),
                () =>
                {
                    if (!TryComp(uid, out MoodComponent? live)
                        || !live.CategorisedEffectTimerGenerations.TryGetValue(prototype.Category, out var active)
                        || active != generation)
                        return;

                    DropMoodlet(uid, prototype.ID, prototype.Category, MoodEffectRemovalReason.Expired);
                });

            return;
        }

        component.UncategorisedEffectTimerGenerations.TryGetValue(prototype.ID, out var soloGeneration);
        soloGeneration += 1;
        component.UncategorisedEffectTimerGenerations[prototype.ID] = soloGeneration;

        Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout),
            () =>
            {
                if (!TryComp(uid, out MoodComponent? live)
                    || !live.UncategorisedEffectTimerGenerations.TryGetValue(prototype.ID, out var active)
                    || active != soloGeneration)
                    return;

                DropMoodlet(uid, prototype.ID, null, MoodEffectRemovalReason.Expired);
            });
    }

    private void DropMoodlet(EntityUid uid, string prototypeId, string? category, MoodEffectRemovalReason reason)
    {
        if (!TryComp<MoodComponent>(uid, out var component))
            return;

        if (category == null)
        {
            if (!component.UncategorisedEffects.Remove(prototypeId))
                return;

            component.UncategorisedEffectTimerGenerations.Remove(prototypeId);
        }
        else
        {
            if (!component.CategorisedEffects.TryGetValue(category, out var currentId)
                || currentId != prototypeId
                || !_protos.HasIndex<MoodEffectPrototype>(currentId))
                return;

            component.CategorisedEffects.Remove(category);
            component.CategorisedEffectTimerGenerations.Remove(category);
        }

        if (reason == MoodEffectRemovalReason.Expired)
            ChainExpiryMoodlet(uid, prototypeId);

        Recalculate(uid, component);
    }

    /// <summary>
    ///     Applies the follow-up moodlet some effects declare for their expiry.
    /// </summary>
    private void ChainExpiryMoodlet(EntityUid uid, string prototypeId)
    {
        if (!_protos.TryIndex<MoodEffectPrototype>(prototypeId, out var proto)
            || proto.MoodletOnEnd is null)
            return;

        RaiseLocalEvent(uid, new MoodEffectEvent(proto.MoodletOnEnd));
    }

    /// <summary>
    ///     Re-sums every active moodlet into the totals and commits a new level.
    /// </summary>
    private void Recalculate(EntityUid uid, MoodComponent component)
    {
        var total = 0f;
        var shown = 0f;

        foreach (var (_, id) in component.CategorisedEffects)
        {
            if (!_protos.TryIndex<MoodEffectPrototype>(id, out var prototype))
                continue;

            total += prototype.MoodChange;
            if (!prototype.Hidden)
                shown += prototype.MoodChange;
        }

        foreach (var (id, value) in component.UncategorisedEffects)
        {
            total += value;

            if (!_protos.TryIndex<MoodEffectPrototype>(id, out var prototype)
                || prototype.Hidden)
                continue;

            shown += value;
        }

        component.CurrentMood = total;
        component.CurrentShownMood = shown;
        CommitLevel(uid, total, component, refresh: true);
    }

    private void CommitLevel(EntityUid uid, float amount, MoodComponent? component = null, bool force = false, bool refresh = false)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled)
            || !Resolve(uid, ref component)
            || component.CurrentMoodThreshold == MoodThreshold.Dead && !refresh)
            return;

        var ev = new OnSetMoodEvent(uid, amount, false);
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled)
            return;

        uid = ev.Receiver;
        amount = ev.MoodChangedAmount;

        if (!Resolve(uid, ref component))
            return;

        var neutral = component.MoodThresholds[MoodThreshold.Neutral];
        var target = amount + neutral + ev.MoodOffset;
        component.CurrentMoodLevel = force
            ? target
            : Math.Clamp(target,
                component.MoodThresholds[MoodThreshold.Dead],
                component.MoodThresholds[MoodThreshold.Insane]);

        if (TryComp<NetMoodComponent>(uid, out var net))
        {
            net.CurrentMoodLevel = component.CurrentMoodLevel;
            net.CurrentMood = component.CurrentMood;
            net.CurrentShownMood = component.CurrentShownMood;
            net.NeutralMoodThreshold = component.MoodThresholds.GetValueOrDefault(MoodThreshold.Neutral);
            net.CurrentSanity = component.CurrentSanity;
            Dirty(uid, net);
        }

        UpdateOverlaySaturation(uid, component.CurrentMoodLevel, component.MoodThresholds[MoodThreshold.Neutral]);
        RefreshThresholdBand(uid, component);
    }

    private void RefreshThresholdBand(EntityUid uid, MoodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var band = ClassifyLevel(component);
        if (band == component.CurrentMoodThreshold)
            return;

        component.CurrentMoodThreshold = band;
        ApplyThresholdBand(uid, component);
    }

    private void ApplyThresholdBand(EntityUid uid, MoodComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component)
            || component.CurrentMoodThreshold == component.LastThreshold && !force)
            return;

        var group = SpeedGroupOf(component.CurrentMoodThreshold);

        _speed.RefreshMovementSpeedModifiers(uid);

        if (group != SpeedGroupOf(component.LastThreshold))
            ShiftCritThreshold(uid, component, group);

        if (FindPriorityAlert(component, out var priority))
            _alerts.ShowAlert(uid, priority);
        else if (component.MoodThresholdsAlerts.TryGetValue(component.CurrentMoodThreshold, out var bandAlert))
            _alerts.ShowAlert(uid, bandAlert);
        else
            _alerts.ClearAlertCategory(uid, component.MoodCategory);

        component.LastThreshold = component.CurrentMoodThreshold;
    }

    private void UpdateOverlaySaturation(EntityUid uid, float mood, float neutralThreshold)
    {
        if (neutralThreshold <= 0f)
            return;

        EnsureComp<SaturationScaleOverlayComponent>(uid, out var overlay);
        overlay.NeutralMoodThreshold = neutralThreshold;
        overlay.SaturationScale = mood / overlay.NeutralMoodThreshold;
        Dirty(uid, overlay);
    }

    private bool FindPriorityAlert(MoodComponent component, out ProtoId<AlertPrototype> alert)
    {
        alert = default;
        var best = float.MinValue;

        foreach (var (_, id) in component.CategorisedEffects)
            ConsiderPriorityAlert(id, ref alert, ref best);

        foreach (var (id, _) in component.UncategorisedEffects)
            ConsiderPriorityAlert(id, ref alert, ref best);

        return best > float.MinValue;
    }

    private bool ConsiderPriorityAlert(string prototypeId, ref ProtoId<AlertPrototype> best, ref float bestMagnitude)
    {
        if (!_protos.TryIndex<MoodEffectPrototype>(prototypeId, out var prototype)
            || prototype.SpecialAlert is not { } special
            || !prototype.SpecialAlertReplace)
            return false;

        var magnitude = MathF.Abs(prototype.MoodChange);
        if (magnitude <= bestMagnitude)
            return false;

        bestMagnitude = magnitude;
        best = special;
        return true;
    }

    private void OnSpeedRefresh(EntityUid uid, MoodComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled)
            || _jetpack.IsUserFlying(uid))
            return;

        var slowdown = _config.GetCVar(CCVars.MoodDecreasesSpeed);
        var speedup = _config.GetCVar(CCVars.MoodIncreasesSpeed);

        var multiplier = component.CurrentMoodThreshold switch
        {
            MoodThreshold.Dead or MoodThreshold.Horrible when slowdown => 0.50f,
            MoodThreshold.Terrible when slowdown => 0.65f,
            MoodThreshold.Bad when slowdown => 0.78f,
            MoodThreshold.Meh when slowdown => 0.90f,
            >= MoodThreshold.Great when speedup => Math.Min(
                MathF.Pow(component.SpeedBonusGrowth,
                    component.CurrentMoodLevel - component.MoodThresholds[MoodThreshold.Neutral]),
                component.MaximumSpeedModifier),
            _ => 1f,
        };

        switch (component.CurrentSanityThreshold)
        {
            case <= SanityThreshold.Crazy:
                multiplier *= 0.9f;
                break;
            case SanityThreshold.Unstable:
                multiplier *= 0.95f;
                break;
        }

        args.ModifySpeed(1, multiplier);
    }

    private void OnMobStateChanged(EntityUid uid, MoodComponent component, MobStateChangedEvent args)
    {
        if (!_config.GetCVar(CCVars.MoodEnabled))
            return;

        if (args.NewMobState == MobState.Dead && args.OldMobState != MobState.Dead)
        {
            RaiseLocalEvent(uid, new MoodEffectEvent("Dead"));
            component.CurrentSanity = component.SanityThresholds[SanityThreshold.Disturbed];
        }
        else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
        {
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Dead"));
            component.CurrentSanity = component.SanityThresholds[SanityThreshold.Disturbed];
            component.CurrentSanityThreshold = SanityThreshold.Disturbed;
            component.LastSanityThreshold = SanityThreshold.Disturbed;
        }

        Recalculate(uid, component);
    }

    private void OnDamageChanged(EntityUid uid, MoodComponent component, DamageChangedEvent args)
    {
        if (HasComp<IgnoreSlowOnDamageComponent>(uid))
        {
            RaiseLocalEvent(uid, new MoodEffectEvent("HealthNoDamage"));
            return;
        }

        var vital = _thresholds.CheckVitalDamage(uid, args.Damageable);
        if (!_thresholds.TryGetPercentageForState(uid, MobState.Critical, vital, out var damage))
            return;

        ProtoId<MoodEffectPrototype> pick = "HealthNoDamage";
        var pickValue = component.HealthMoodEffectsThresholds["HealthNoDamage"];

        foreach (var (id, value) in component.HealthMoodEffectsThresholds)
        {
            if (value > damage || value < pickValue)
                continue;

            pick = id;
            pickValue = value;
        }

        RaiseLocalEvent(uid, new MoodEffectEvent(pick));
    }

    private void OnCuffedChanged(EntityUid uid, MoodComponent component, ref CuffedStateChangeEvent args)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable) || cuffable.CuffedHandCount <= 0)
        {
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Handcuffed"));
            return;
        }

        RaiseLocalEvent(uid, new MoodEffectEvent("Handcuffed"));
    }

    private void OnSuffocationStarted(EntityUid uid, MoodComponent component, ref SuffocationEvent args)
    {
        RaiseLocalEvent(uid, new MoodEffectEvent("Suffocating"));
    }

    private void OnSuffocationStopped(EntityUid uid, MoodComponent component, ref StopSuffocatingEvent args)
    {
        RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Suffocating"));
    }

    private void OnIgnited(EntityUid uid, MoodComponent component, ref IgnitedEvent args)
    {
        RaiseLocalEvent(uid, new MoodEffectEvent("OnFire"));
    }

    private void OnExtinguished(EntityUid uid, MoodComponent component, ref ExtinguishedEvent args)
    {
        RaiseLocalEvent(uid, new MoodRemoveEffectEvent("OnFire"));
    }

    private void OnSlipped(ref SlipEvent args)
    {
        if (!HasComp<MoodComponent>(args.Slipped))
            return;

        RaiseLocalEvent(args.Slipped, new MoodEffectEvent("MobSlipped"));
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.OwnedEntity is not { } owned || !HasComp<MoodComponent>(owned))
            return;

        if (args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<TraitorRoleComponent>))
            RaiseLocalEvent(owned, new MoodEffectEvent("TraitorFocused"));

        if (args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<RevolutionaryRoleComponent>))
            RaiseLocalEvent(owned, new MoodEffectEvent("RevolutionFocused"));

        if (args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<CosmicCultRoleComponent>))
            RaiseLocalEvent(owned, new MoodEffectEvent("CultFocused"));
    }

    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (args.Mind.OwnedEntity is not { } owned || !HasComp<MoodComponent>(owned))
            return;

        if (!args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<TraitorRoleComponent>))
            RaiseLocalEvent(owned, new MoodRemoveEffectEvent("TraitorFocused"));

        if (!args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<RevolutionaryRoleComponent>))
            RaiseLocalEvent(owned, new MoodRemoveEffectEvent("RevolutionFocused"));

        if (!args.Mind.MindRoleContainer.ContainedEntities.Any(HasComp<CosmicCultRoleComponent>))
            RaiseLocalEvent(owned, new MoodRemoveEffectEvent("CultFocused"));
    }

    private void OnSatiationUpdated(EntityUid uid, MoodComponent component, ref SatiationUpdateEvent args)
    {
        SyncSatiationMoodlet(uid, args.Type);
    }

    private void OnVomitRelayed(EntityUid uid, MoodComponent component, ref MoodVomitEvent args)
    {
        RaiseLocalEvent(uid, new MoodEffectEvent("MobVomit"));
    }

    private void OnCreamPiedRelayed(EntityUid uid, MoodComponent component, ref MoodCreamPiedEvent args)
    {
        if (args.CreamPied)
            RaiseLocalEvent(uid, new MoodEffectEvent("Creampied"));
        else
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Creampied"));
    }

    /// <summary>
    ///     Mirrors the current hunger/thirst band into its category moodlet.
    /// </summary>
    private void SyncSatiationMoodlet(EntityUid uid, ProtoId<SatiationTypePrototype> type)
    {
        if (!TryComp<SatiationComponent>(uid, out var satiation))
            return;

        var table = type == SatiationSystem.Hunger
            ? HungerMoodlets
            : type == SatiationSystem.Thirst
                ? ThirstMoodlets
                : null;

        if (table == null
            || !_satiation.TryGetValueByThreshold((uid, satiation), type, table, out var moodlet, out _, out _))
            return;

        RaiseLocalEvent(uid, new MoodEffectEvent(moodlet.Id));
    }

    /// <summary>
    ///     Tracks hazardous pressure with moodlets, using felt pressure
    ///     so protective gear is respected.
    /// </summary>
    private void TickPressure(EntityUid uid)
    {
        if (!TryComp<BarotraumaComponent>(uid, out var barotrauma)
            || _atmo.GetContainingMixture(uid) is not { } mixture)
            return;

        var felt = MathF.Max(mixture.Pressure, 1f);
        felt = felt switch
        {
            <= Atmospherics.WarningLowPressure => _barotrauma.GetFeltLowPressure(uid, barotrauma, felt),
            >= Atmospherics.WarningHighPressure => _barotrauma.GetFeltHighPressure(uid, barotrauma, felt),
            _ => felt,
        };

        if (felt <= Atmospherics.WarningLowPressure)
        {
            RaiseLocalEvent(uid, new MoodEffectEvent("MobLowPressure"));
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("MobHighPressure"));
        }
        else if (felt >= Atmospherics.WarningHighPressure)
        {
            RaiseLocalEvent(uid, new MoodEffectEvent("MobHighPressure"));
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("MobLowPressure"));
        }
        else
        {
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("MobLowPressure"));
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("MobHighPressure"));
        }
    }

    private void TickSanity(EntityUid uid, MoodComponent component, float frameTime)
    {
        if (component.CurrentMoodThreshold == MoodThreshold.Dead
            || !component.SanityDeltaPerSecond.TryGetValue(component.CurrentMoodThreshold, out var drift))
            return;

        var target = component.MaxSanity;
        if (drift < 0f)
        {
            target = component.CurrentMoodThreshold switch
            {
                MoodThreshold.Insane or MoodThreshold.Horrible => component.MinSanity,
                MoodThreshold.Terrible => component.SanityThresholds[SanityThreshold.Crazy],
                _ => component.UnstableFloorSanity,
            };
        }
        else
        {
            target = component.CurrentMoodThreshold switch
            {
                <= MoodThreshold.Great => component.UnstableFloorSanity,
                MoodThreshold.Exceptional => component.SanityThresholds[SanityThreshold.Disturbed],
                _ => target,
            };
        }

        SettleSanity(uid, component, component.CurrentSanity + drift * frameTime, target, frameTime);
    }

    private void SettleSanity(EntityUid uid, MoodComponent component, float value, float target, float frameTime)
    {
        var boundedTarget = Math.Clamp(target, component.MinSanity, component.MaxSanity);
        var settled = Math.Clamp(value, component.MinSanity, component.MaxSanity);

        if (boundedTarget > component.CurrentSanity && settled < boundedTarget)
        {
            var boost = Math.Max(component.SanityRecoveryRate * frameTime, 0f);
            settled = Math.Min(settled + boost, boundedTarget);
        }

        if (Math.Abs(settled - component.CurrentSanity) < 0.001f)
            return;

        component.CurrentSanity = settled;
        component.CurrentSanityThreshold = ClassifySanity(component);

        if (component.CurrentSanityThreshold != component.LastSanityThreshold)
        {
            _speed.RefreshMovementSpeedModifiers(uid);
            component.LastSanityThreshold = component.CurrentSanityThreshold;
        }

        if (!TryComp<NetMoodComponent>(uid, out var net))
            return;

        net.CurrentSanity = component.CurrentSanity;
        Dirty(uid, net);
    }

    private static SanityThreshold ClassifySanity(MoodComponent component)
    {
        if (component.CurrentSanity <= component.SanityThresholds[SanityThreshold.Insane])
            return SanityThreshold.Insane;

        if (component.CurrentSanity <= component.SanityThresholds[SanityThreshold.Crazy])
            return SanityThreshold.Crazy;

        if (component.CurrentSanity <= component.SanityThresholds[SanityThreshold.Unstable])
            return SanityThreshold.Unstable;

        return component.CurrentSanity <= component.SanityThresholds[SanityThreshold.Disturbed]
            ? SanityThreshold.Disturbed
            : SanityThreshold.Great;
    }

    private static MoodThreshold ClassifyLevel(MoodComponent component, float? level = null)
    {
        level ??= component.CurrentMoodLevel;
        var band = MoodThreshold.Dead;
        var ceiling = component.MoodThresholds[MoodThreshold.Insane];

        foreach (var (threshold, value) in component.MoodThresholds)
        {
            if (value > ceiling || value < level)
                continue;

            band = threshold;
            ceiling = value;
        }

        return band;
    }

    /// <summary>
    ///     Collapses bands into three crit groups: good (+1), neutral (0), bad (-1).
    /// </summary>
    private static int SpeedGroupOf(MoodThreshold threshold)
    {
        return threshold switch
        {
            >= MoodThreshold.Good => 1,
            <= MoodThreshold.Meh => -1,
            _ => 0,
        };
    }

    private void ShiftCritThreshold(EntityUid uid, MoodComponent component, int group)
    {
        if (!_config.GetCVar(CCVars.MoodModifiesThresholds)
            || !TryComp<MobThresholdsComponent>(uid, out var mobThresholds)
            || !CacheBaselineThresholds(uid, component, mobThresholds))
            return;

        var scale = group switch
        {
            1 => component.IncreaseCritThreshold,
            -1 => component.DecreaseCritThreshold,
            _ => 1f,
        };

        var crit = component.CritThresholdBeforeModify.Float() * scale;
        var dead = component.DeadThresholdBeforeModify.Float() * scale;

        dead = MathF.Max(dead, crit + 0.01f);

        _thresholds.SetMobStateThreshold(uid, FixedPoint2.New(crit), MobState.Critical, mobThresholds);
        _thresholds.SetMobStateThreshold(uid, FixedPoint2.New(dead), MobState.Dead, mobThresholds);
    }

    private bool CacheBaselineThresholds(EntityUid uid, MoodComponent component, MobThresholdsComponent thresholds)
    {
        if (component.CritThresholdBeforeModify != default
            && component.DeadThresholdBeforeModify != default)
            return true;

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit, thresholds)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds))
            return false;

        component.CritThresholdBeforeModify = crit.Value;
        component.DeadThresholdBeforeModify = dead.Value;
        return true;
    }

    private void RestoreBaselineThresholds(EntityUid uid, MoodComponent component)
    {
        if (!_config.GetCVar(CCVars.MoodModifiesThresholds)
            || !TryComp<MobThresholdsComponent>(uid, out var thresholds)
            || !CacheBaselineThresholds(uid, component, thresholds))
            return;

        _thresholds.SetMobStateThreshold(uid, component.CritThresholdBeforeModify, MobState.Critical, thresholds);
        _thresholds.SetMobStateThreshold(uid, component.DeadThresholdBeforeModify, MobState.Dead, thresholds);
    }

    private void OnMoodAlertShown(EntityUid uid, MoodComponent component, ShowMoodAlertEvent args)
    {
        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var text = DescribeMood(uid, component);
        _chat.ChatMessageToOne(ChatChannel.Emotes, text, text, EntityUid.Invalid, false, session.Channel);
    }

    private string DescribeMood(EntityUid uid, MoodComponent component)
    {
        var text = "[examineborder]";
        text += Loc.GetString("mood-show-effects-start");
        text += $"\n[color=#282D31]{Loc.GetString("examine-border-line")}[/color]";

#if DEBUG
        var sanity = Loc.GetString("mood-show-sanity-line", ("sanity", MathF.Round(component.CurrentSanity, 1)));
        text += $"\n{sanity}";
#endif

        var shown = false;

        foreach (var (_, id) in component.CategorisedEffects)
        {
            if (!_protos.TryIndex<MoodEffectPrototype>(id, out var prototype)
                || prototype.Hidden)
                continue;

            shown = true;
            var color = prototype.MoodChange > 0 ? "#008000" : "#BA0000";
            text += $"\n[font size=10][color={color}]{prototype.Description(uid)}[/color][/font]";
        }

        foreach (var (id, _) in component.UncategorisedEffects)
        {
            if (!_protos.TryIndex<MoodEffectPrototype>(id, out var prototype)
                || prototype.Hidden)
                continue;

            shown = true;
            var color = prototype.MoodChange > 0 ? "#008000" : "#BA0000";
            text += $"\n[font size=10][color={color}]{prototype.Description(uid)}[/color][/font]";
        }

        if (!shown)
            text += $"\n[font size=10][color=#808080]{Loc.GetString("mood-show-no-effects")}[/color][/font]";

        text += Loc.GetString("mood-show-effects-end");
        text += "[/examineborder]";
        return text;
    }
}

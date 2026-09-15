// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Mood;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Server._Onyx.Mood;

/// <summary>
///     Server-side mood state: active moodlets plus the derived totals
///     the system recomputes from them. Pure data; see <see cref="MoodSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class MoodComponent : Component
{
    /// <summary>
    ///     Sum of all active moodlets, hidden included.
    /// </summary>
    [DataField]
    public float CurrentMood;

    /// <summary>
    ///     Sum of visible moodlets only.
    /// </summary>
    [DataField]
    public float CurrentShownMood;

    /// <summary>
    ///     Neutral-shifted level compared against <see cref="MoodThresholds"/>.
    /// </summary>
    [DataField]
    public float CurrentMoodLevel;

    /// <summary>
    ///     Threshold band the level currently falls into.
    /// </summary>
    [DataField]
    public MoodThreshold CurrentMoodThreshold;

    /// <summary>
    ///     Band used the last time threshold effects were applied.
    /// </summary>
    [DataField]
    public MoodThreshold LastThreshold;

    /// <summary>
    ///     Active moodlet per category slot.
    /// </summary>
    public readonly Dictionary<string, string> CategorisedEffects = new();

    /// <summary>
    ///     Generation counters invalidating stale category expiry timers.
    /// </summary>
    public readonly Dictionary<string, int> CategorisedEffectTimerGenerations = new();

    /// <summary>
    ///     Stacking moodlets with their applied (modifier-scaled) values.
    /// </summary>
    public readonly Dictionary<string, float> UncategorisedEffects = new();

    /// <summary>
    ///     Generation counters invalidating stale stacking-effect expiry timers.
    /// </summary>
    public readonly Dictionary<string, int> UncategorisedEffectTimerGenerations = new();

    /// <summary>
    ///     Current sanity points.
    /// </summary>
    [DataField]
    public float CurrentSanity = 100f;

    [DataField]
    public float MinSanity;

    [DataField]
    public float MaxSanity = 150f;

    /// <summary>
    ///     Extra recovery applied per second while sanity climbs toward its target.
    /// </summary>
    [DataField]
    public float SanityRecoveryRate = 42f;

    /// <summary>
    ///     Floor sanity settles at while mood is merely low instead of awful.
    /// </summary>
    [DataField]
    public float UnstableFloorSanity = 50f;

    [DataField]
    public SanityThreshold CurrentSanityThreshold = SanityThreshold.Disturbed;

    [DataField]
    public SanityThreshold LastSanityThreshold = SanityThreshold.Disturbed;

    /// <summary>
    ///     Geometric base for the high-mood speed bonus:
    ///     bonus = growth ^ (level - neutral). Tune in thousandths.
    /// </summary>
    [DataField]
    public float SpeedBonusGrowth = 1.003f;

    /// <summary>
    ///     Hard floor for the low-mood slowdown multiplier.
    /// </summary>
    [DataField]
    public float MinimumSpeedModifier = 0.75f;

    /// <summary>
    ///     Hard ceiling for the high-mood speedup multiplier.
    /// </summary>
    [DataField]
    public float MaximumSpeedModifier = 1.15f;

    /// <summary>
    ///     Crit-threshold scale applied while mood is good.
    /// </summary>
    [DataField]
    public float IncreaseCritThreshold = 1.2f;

    /// <summary>
    ///     Crit-threshold scale applied while mood is bad.
    /// </summary>
    [DataField]
    public float DecreaseCritThreshold = 0.9f;

    public FixedPoint2 CritThresholdBeforeModify;
    public FixedPoint2 DeadThresholdBeforeModify;

    [DataField]
    public ProtoId<AlertCategoryPrototype> MoodCategory = "Mood";

    [DataField(customTypeSerializer: typeof(DictionarySerializer<MoodThreshold, float>))]
    public Dictionary<MoodThreshold, float> MoodThresholds = new()
    {
        { MoodThreshold.Insane, 120f },
        { MoodThreshold.Perfect, 100f },
        { MoodThreshold.Exceptional, 80f },
        { MoodThreshold.Great, 70f },
        { MoodThreshold.Good, 60f },
        { MoodThreshold.Neutral, 50f },
        { MoodThreshold.Meh, 40f },
        { MoodThreshold.Bad, 30f },
        { MoodThreshold.Terrible, 20f },
        { MoodThreshold.Horrible, 10f },
        { MoodThreshold.Dead, 0f },
    };

    [DataField(customTypeSerializer: typeof(DictionarySerializer<MoodThreshold, ProtoId<AlertPrototype>>))]
    public Dictionary<MoodThreshold, ProtoId<AlertPrototype>> MoodThresholdsAlerts = new()
    {
        { MoodThreshold.Dead, "MoodDead" },
        { MoodThreshold.Horrible, "Horrible" },
        { MoodThreshold.Terrible, "Terrible" },
        { MoodThreshold.Bad, "Bad" },
        { MoodThreshold.Meh, "Meh" },
        { MoodThreshold.Neutral, "Neutral" },
        { MoodThreshold.Good, "Good" },
        { MoodThreshold.Great, "Great" },
        { MoodThreshold.Exceptional, "Exceptional" },
        { MoodThreshold.Perfect, "Perfect" },
        { MoodThreshold.Insane, "Insane" },
    };

    [DataField(customTypeSerializer: typeof(DictionarySerializer<SanityThreshold, float>))]
    public Dictionary<SanityThreshold, float> SanityThresholds = new()
    {
        { SanityThreshold.Great, 125f },
        { SanityThreshold.Disturbed, 100f },
        { SanityThreshold.Unstable, 75f },
        { SanityThreshold.Crazy, 50f },
        { SanityThreshold.Insane, 25f },
    };

    [DataField(customTypeSerializer: typeof(DictionarySerializer<MoodThreshold, float>))]
    public Dictionary<MoodThreshold, float> SanityDeltaPerSecond = new()
    {
        { MoodThreshold.Insane, -0.30f },
        { MoodThreshold.Perfect, 0.60f },
        { MoodThreshold.Exceptional, 0.40f },
        { MoodThreshold.Great, 0.30f },
        { MoodThreshold.Good, 0.20f },
        { MoodThreshold.Neutral, 0f },
        { MoodThreshold.Meh, -0.05f },
        { MoodThreshold.Bad, -0.10f },
        { MoodThreshold.Terrible, -0.15f },
        { MoodThreshold.Horrible, -0.30f },
        { MoodThreshold.Dead, 0f },
    };

    /// <summary>
    ///     Health moodlets keyed by fraction of the critical threshold.
    /// </summary>
    [DataField(customTypeSerializer: typeof(DictionarySerializer<ProtoId<MoodEffectPrototype>, float>))]
    public Dictionary<ProtoId<MoodEffectPrototype>, float> HealthMoodEffectsThresholds = new()
    {
        { "HealthHeavyDamage", 0.8f },
        { "HealthSevereDamage", 0.5f },
        { "HealthLightDamage", 0.1f },
        { "HealthNoDamage", 0.05f },
    };
}

[Serializable]
public enum MoodThreshold : ushort
{
    Dead = 0,
    Horrible = 1,
    Terrible = 2,
    Bad = 3,
    Meh = 4,
    Neutral = 5,
    Good = 6,
    Great = 7,
    Exceptional = 8,
    Perfect = 9,
    Insane = 10,
}

[Serializable]
public enum SanityThreshold : ushort
{
    Insane = 0,
    Crazy = 1,
    Unstable = 2,
    Disturbed = 3,
    Great = 4,
}

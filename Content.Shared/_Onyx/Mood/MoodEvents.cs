// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Alert;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Mood;

/// <summary>
///     Request to apply a moodlet to an entity.
///     Modifier/offset scale the prototype's shift for uncategorized moodlets.
/// </summary>
[Serializable, NetSerializable]
public sealed class MoodEffectEvent : EntityEventArgs
{
    public string EffectId;
    public float EffectModifier;
    public float EffectOffset;

    public MoodEffectEvent(string effectId, float effectModifier = 1f, float effectOffset = 0f)
    {
        EffectId = effectId;
        EffectModifier = effectModifier;
        EffectOffset = effectOffset;
    }
}

/// <summary>
///     Request to lift a moodlet before its timeout.
/// </summary>
[Serializable, NetSerializable]
public sealed class MoodRemoveEffectEvent : EntityEventArgs
{
    public string EffectId;
    public MoodEffectRemovalReason Reason;

    public MoodRemoveEffectEvent(string effectId, MoodEffectRemovalReason reason = MoodEffectRemovalReason.Manual)
    {
        EffectId = effectId;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public enum MoodEffectRemovalReason : byte
{
    Manual = 0,
    Expired = 1,
}

/// <summary>
///     Request to clear timed moodlets, optionally including permanent ones.
/// </summary>
[Serializable, NetSerializable]
public sealed class MoodPurgeEffectsEvent : EntityEventArgs
{
    public bool RemovePermanentMoodlets;

    public MoodPurgeEffectsEvent(bool removePermanentMoodlets)
    {
        RemovePermanentMoodlets = removePermanentMoodlets;
    }
}

/// <summary>
///     Raised when the final mood total is committed, so traits or other
///     systems can scale or cancel the result before thresholds update.
/// </summary>
[ByRefEvent]
public record struct OnSetMoodEvent(EntityUid Receiver, float MoodChangedAmount, bool Cancelled, float MoodOffset = 0f);

/// <summary>
///     Raised before a moodlet is applied, allowing modifiers to swap
///     the effect id or rescale it.
/// </summary>
[ByRefEvent]
public record struct OnMoodEffect(EntityUid Receiver, string EffectId, float EffectModifier = 1f, float EffectOffset = 0f);

public sealed partial class ShowMoodAlertEvent : BaseAlertEvent;

/// <summary>
///     Relayed when the entity vomits, so mood can react without
///     depending on medical internals.
/// </summary>
[ByRefEvent]
public readonly record struct MoodVomitEvent;

/// <summary>
///     Relayed when the creampied visual state flips.
/// </summary>
[ByRefEvent]
public readonly record struct MoodCreamPiedEvent(bool CreamPied);

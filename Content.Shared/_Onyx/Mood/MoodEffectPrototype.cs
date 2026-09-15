// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mood;

/// <summary>
///     Single moodlet definition: how much it shifts mood, how long it lasts,
///     whether the player sees it, and what follows it when it expires.
/// </summary>
[Prototype]
public sealed partial class MoodEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Category slot this moodlet occupies. Null means it stacks freely
    ///     with everything else instead of replacing a sibling.
    /// </summary>
    [DataField]
    public ProtoId<MoodCategoryPrototype>? Category;

    /// <summary>
    ///     Flat mood shift applied while the moodlet is active.
    /// </summary>
    [DataField(required: true)]
    public float MoodChange;

    /// <summary>
    ///     Lifetime in seconds. Zero means it persists until removed explicitly.
    /// </summary>
    [DataField]
    public int Timeout;

    /// <summary>
    ///     Hidden moodlets affect totals silently: no popup, no examine line.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    ///     Follow-up moodlet applied once this one expires (addiction-style chains).
    /// </summary>
    [DataField]
    public ProtoId<MoodEffectPrototype>? MoodletOnEnd;

    /// <summary>
    ///     Alert icon forced while this moodlet is active, if replacement is allowed.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype>? SpecialAlert;

    /// <summary>
    ///     Whether <see cref="SpecialAlert"/> overrides the threshold-based icon.
    /// </summary>
    [DataField]
    public bool SpecialAlertReplace;

    public string Description(EntityUid? entity = null)
    {
        var target = entity ?? EntityUid.Invalid;
        return Loc.GetString($"mood-effect-{ID}", ("entity", target));
    }
}

// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mood;

/// <summary>
///     Slot grouping for moodlets where only one effect per slot stays active.
///     Applying another effect of the same category replaces the previous one.
/// </summary>
[Prototype]
public sealed partial class MoodCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}

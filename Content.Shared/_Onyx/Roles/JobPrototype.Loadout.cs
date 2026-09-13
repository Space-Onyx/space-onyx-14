// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>
    /// Shows this job in loadout selection without enabling round-start job preferences.
    /// </summary>
    [DataField]
    public bool ShowInLoadout;
}

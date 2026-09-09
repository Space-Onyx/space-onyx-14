// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared.Traits;

public sealed partial class TraitCategoryPrototype
{
    /// <summary>
    /// Maximum number of traits selectable from this category.
    /// </summary>
    [DataField]
    public int? MaxTraits;
}

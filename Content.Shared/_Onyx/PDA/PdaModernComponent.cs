// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.PDA;

[RegisterComponent]
public sealed partial class PdaModernComponent : Component
{
    /// <summary>
    /// Brand text in the Modern PDA sidebar header. Null or empty hides it.
    /// </summary>
    [DataField]
    public string? SidebarBrand = "NT";

    /// <summary>
    /// Accent themes selectable in the Modern PDA settings. A single entry locks the choice.
    /// </summary>
    [DataField]
    public List<ProtoId<PdaThemePrototype>> ThemePresets = new()
    {
        "PdaThemeCivilian",
        "PdaThemeEngineering",
        "PdaThemeMedical",
        "PdaThemeScience",
        "PdaThemeSecurity",
        "PdaThemeCargo",
        "PdaThemeCommand",
    };
}

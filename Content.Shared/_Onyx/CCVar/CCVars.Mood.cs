// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Master switch for the mood simulation.
    /// </summary>
    public static readonly CVarDef<bool> MoodEnabled =
        CVarDef.Create("mood.enabled", true, CVar.SERVER);

    /// <summary>
    ///     Whether good mood may speed the entity up.
    /// </summary>
    public static readonly CVarDef<bool> MoodIncreasesSpeed =
        CVarDef.Create("mood.increases_speed", false, CVar.SERVER);

    /// <summary>
    ///     Whether bad mood may slow the entity down.
    /// </summary>
    public static readonly CVarDef<bool> MoodDecreasesSpeed =
        CVarDef.Create("mood.decreases_speed", true, CVar.SERVER);

    /// <summary>
    ///     Whether mood shifts the mob crit thresholds.
    /// </summary>
    public static readonly CVarDef<bool> MoodModifiesThresholds =
        CVarDef.Create("mood.modify_thresholds", false, CVar.SERVER);

    /// <summary>
    ///     Client-side saturation visuals following mood.
    /// </summary>
    public static readonly CVarDef<bool> MoodVisualEffects =
        CVarDef.Create("mood.visual_effects", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}

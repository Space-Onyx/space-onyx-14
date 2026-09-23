// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ToggleSprint =
        CVarDef.Create("control.toggle_sprint", false, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> SprintUntilStop =
        CVarDef.Create("control.sprint_until_stop", true, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);
}

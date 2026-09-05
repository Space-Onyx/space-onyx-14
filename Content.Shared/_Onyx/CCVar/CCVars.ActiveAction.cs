// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ActiveActionEnabled =
        CVarDef.Create("ic.active_action_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}

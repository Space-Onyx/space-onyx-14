// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences.Loadouts;

namespace Content.Shared._Onyx.Silicons.Laws;

[ByRefEvent]
public readonly record struct ApplySyntheticLawPresetEvent(RoleLoadout Loadout);

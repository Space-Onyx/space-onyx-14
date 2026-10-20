// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Loadouts;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared.Station.Systems;

public sealed partial class StationSpawningSystem
{
    public void ApplyLoadoutCustomization(EntityUid entity, Loadout? loadout)
    {
        if (loadout == null)
            return;

        if (!string.IsNullOrWhiteSpace(loadout.CustomName))
            _metadata.SetEntityName(entity, loadout.CustomName);

        if (!string.IsNullOrWhiteSpace(loadout.CustomDescription))
            _metadata.SetEntityDescription(entity, loadout.CustomDescription);

        if (string.IsNullOrEmpty(loadout.CustomColorTint) || !Color.TryFromHex(loadout.CustomColorTint, out var color))
            return;

        var tint = EnsureComp<LoadoutTintComponent>(entity);
        tint.Color = color;
        Dirty(entity, tint);
    }
}

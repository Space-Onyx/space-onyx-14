// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Silicons.Laws;
using Content.Shared._Onyx.Silicons.Laws;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    [Dependency] private SiliconLawSystem _siliconLaws = default!;

    private void ApplySyntheticLawPreset(EntityUid entity, RoleLoadout loadout)
    {
        if (loadout.SyntheticLawPreset is not { } presetId ||
            !ProtoMan.TryIndex(presetId, out SyntheticLawPresetPrototype? preset) ||
            !preset.Roles.Contains(loadout.Role) ||
            !TryComp(entity, out SiliconLawProviderComponent? provider))
        {
            return;
        }

        _siliconLaws.SetProviderLawset((entity, provider), preset.Lawset);
    }
}

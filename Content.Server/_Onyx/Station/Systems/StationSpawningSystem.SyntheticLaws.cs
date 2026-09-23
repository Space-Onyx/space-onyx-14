// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Silicons.Laws;
using Content.Shared._Onyx.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server.Station.Systems;

public sealed partial class SyntheticLawPresetSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _siliconLaws = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetaDataComponent, ApplySyntheticLawPresetEvent>(OnApplySyntheticLawPreset);
    }

    private void OnApplySyntheticLawPreset(Entity<MetaDataComponent> entity, ref ApplySyntheticLawPresetEvent args)
    {
        if (args.Loadout.SyntheticLawPreset is not { } presetId ||
            !ProtoMan.TryIndex(presetId, out SyntheticLawPresetPrototype? preset) ||
            !preset.Roles.Contains(args.Loadout.Role) ||
            !TryComp(entity, out SiliconLawProviderComponent? provider))
        {
            return;
        }

        _siliconLaws.SetProviderLawset((entity.Owner, provider), preset.Lawset);
    }
}

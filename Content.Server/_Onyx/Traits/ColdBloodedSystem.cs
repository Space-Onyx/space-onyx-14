// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Traits;

namespace Content.Server._Onyx.Traits;

public sealed class ColdBloodedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColdBloodedComponent, ModifyThermalRegulationEvent>(OnModifyThermalRegulation);
    }

    private void OnModifyThermalRegulation(Entity<ColdBloodedComponent> ent, ref ModifyThermalRegulationEvent args)
    {
        args.MetabolismHeatMultiplier *= ent.Comp.MetabolismHeatMultiplier;
        args.ImplicitHeatingMultiplier = 0f;
        args.ShiveringMultiplier = 0f;
    }
}

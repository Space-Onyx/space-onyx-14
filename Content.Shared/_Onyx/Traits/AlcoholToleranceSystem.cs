// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Drunk;

namespace Content.Shared._Onyx.Traits;

public sealed class AlcoholToleranceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlcoholToleranceComponent, SharedDrunkSystem.DrunkEvent>(OnDrunk);
    }

    private void OnDrunk(Entity<AlcoholToleranceComponent> ent, ref SharedDrunkSystem.DrunkEvent args)
    {
        args.Duration *= ent.Comp.DrunkDurationMultiplier;
    }
}

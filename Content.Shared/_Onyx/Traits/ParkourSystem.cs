// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Traits;

public sealed class ParkourSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ParkourComponent, GetClimbDelayEvent>(OnGetClimbDelay);
    }

    private void OnGetClimbDelay(Entity<ParkourComponent> ent, ref GetClimbDelayEvent args)
    {
        args.Delay *= ent.Comp.ClimbDelayMultiplier;
    }
}

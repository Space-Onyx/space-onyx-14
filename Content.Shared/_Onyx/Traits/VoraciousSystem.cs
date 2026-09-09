// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Traits;

public sealed class VoraciousSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VoraciousComponent, GetEatingDelayEvent>(OnGetEatingDelay);
    }

    private void OnGetEatingDelay(Entity<VoraciousComponent> ent, ref GetEatingDelayEvent args)
    {
        args.Delay *= ent.Comp.EatingDelayMultiplier;
    }
}

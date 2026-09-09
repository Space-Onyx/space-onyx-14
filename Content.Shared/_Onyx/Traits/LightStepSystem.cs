// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Traits;

public sealed class LightStepSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LightStepComponent, ModifyFootstepVolumeEvent>(OnModifyFootstepVolume);
    }

    private void OnModifyFootstepVolume(Entity<LightStepComponent> ent, ref ModifyFootstepVolumeEvent args)
    {
        args.Modifier += ent.Comp.FootstepVolumeModifier;
    }
}

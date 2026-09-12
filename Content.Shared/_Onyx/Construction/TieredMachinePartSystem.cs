// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Examine;

namespace Content.Shared._Onyx.Construction;

public sealed class TieredMachinePartSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TieredMachinePartComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<TieredMachinePartComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("tiered-machine-part-examine", ("tier", ent.Comp.Tier)));
    }
}

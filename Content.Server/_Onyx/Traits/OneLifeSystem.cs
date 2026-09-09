// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Traits;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;

namespace Content.Server._Onyx.Traits;

public sealed partial class OneLifeSystem : EntitySystem
{
    [Dependency] private GibbingSystem _gibbing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OneLifeComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<OneLifeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _gibbing.Gib(ent, dropGiblets: false);
    }
}

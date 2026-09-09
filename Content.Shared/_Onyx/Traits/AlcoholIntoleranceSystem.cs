// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;

namespace Content.Shared._Onyx.Traits;

public sealed partial class AlcoholIntoleranceSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlcoholIntoleranceComponent, SharedDrunkSystem.DrunkEvent>(OnDrunk);
    }

    private void OnDrunk(Entity<AlcoholIntoleranceComponent> ent, ref SharedDrunkSystem.DrunkEvent args)
    {
        var amount = (float) args.Duration.TotalSeconds * ent.Comp.PoisonPerSecondOfDrunkenness;
        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Poison"] = amount } },
            interruptsDoAfters: false);
    }
}

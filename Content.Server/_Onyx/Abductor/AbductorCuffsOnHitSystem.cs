// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared._Onyx.Abductor;
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Onyx.Abductor;

public sealed partial class AbductorCuffsOnHitSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedCuffableSystem _cuffs = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CuffsOnHitComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<CuffsOnHitComponent, CuffsOnHitDoAfter>(OnFinished);
    }

    private void OnHit(Entity<CuffsOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<CuffableComponent>(target, out var cuffable) || cuffable.Container.Count != 0)
                continue;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Duration, new CuffsOnHitDoAfter(), ent, target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                DistanceThreshold = 1f,
            });
        }
    }

    private void OnFinished(Entity<CuffsOnHitComponent> ent, ref CuffsOnHitDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.HandcuffPrototype is not { } handcuffPrototype || !TryComp<CuffableComponent>(target, out var cuffable) || cuffable.Container.Count != 0)
            return;

        args.Handled = true;
        var cuffs = SpawnNextToOrDrop(handcuffPrototype, args.User);
        if (!_cuffs.TryAddNewCuffs(target, args.User, cuffs, cuffable))
            QueueDel(cuffs);
    }
}

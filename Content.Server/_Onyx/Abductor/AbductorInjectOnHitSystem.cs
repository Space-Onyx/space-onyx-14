// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Abductor;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Abductor;

public sealed partial class AbductorInjectOnHitSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedCuffableSystem _cuffs = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InjectOnHitComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<InjectOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!_solutions.TryGetInjectableSolution(target, out _, out _)
                || ExceedsLimit(ent.Comp, target))
                continue;

            if (ent.Comp.NeedsRestrain && !IsRestrained(target))
                Timer.Spawn(ent.Comp.InjectionDelay, () => Inject(ent.Comp, target));
            else
                Inject(ent.Comp, target);

            _color.RaiseEffect(Color.Blue, [target], Filter.Pvs(target, entityManager: EntityManager));
            if (ent.Comp.Sound is not null)
                _audio.PlayPvs(ent.Comp.Sound, target);
        }
    }

    private bool ExceedsLimit(InjectOnHitComponent comp, EntityUid target)
    {
        if (comp.Limit is not { } limit)
            return false;

        foreach (var reagent in comp.Reagents)
        {
            if (_solutions.GetTotalPrototypeQuantity(target, reagent.Reagent.Prototype) >= FixedPoint2.New(limit))
                return true;
        }

        return false;
    }

    private bool IsRestrained(EntityUid target)
    {
        return _mobState.IsIncapacitated(target)
            || HasComp<StunnedComponent>(target)
            || HasComp<KnockedDownComponent>(target)
            || TryComp<CuffableComponent>(target, out var cuffable) && _cuffs.IsCuffed((target, cuffable));
    }

    private void Inject(InjectOnHitComponent comp, EntityUid target)
    {
        if (Deleted(target) || !_solutions.TryGetInjectableSolution(target, out var solutionEnt, out _))
            return;

        var solution = new Solution(comp.Reagents);
        _reactive.DoEntityReaction(target, solution, ReactionMethod.Injection);
        _solutions.TryAddSolution(solutionEnt.Value, solution);
    }
}

// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;

namespace Content.Shared._Onyx.Salvage.Bosses.MobPhases;

public sealed partial class MobPhasesSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobPhasesComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<MobPhasesComponent, DamageDealtEvent>(OnDamage);
    }

    private void OnInit(Entity<MobPhasesComponent> ent, ref MapInitEvent args)
        => ent.Comp.PhaseThresholds = ent.Comp.BasePhaseThresholds;

    private void OnDamage(Entity<MobPhasesComponent> ent, ref DamageDealtEvent args)
    {
        ent.Comp.AccumulatedDamage = FixedPoint2.Max(0, ent.Comp.AccumulatedDamage + args.Damage.GetTotal());
        Dirty(ent);
        UpdatePhases(ent.Owner);
    }

    /// <summary>
    /// Updates current phase according to its thresholds.
    /// </summary>
    [PublicAPI]
    public void UpdatePhases(Entity<MobPhasesComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var ai = ent.Comp;
        foreach (var (threshold, phase) in ai.PhaseThresholds.Reverse())
        {
            if (ai.AccumulatedDamage < threshold)
                continue;

            if (phase < ent.Comp.CurrentPhase
                && !ai.CanSwitchBack)
                continue;

            ent.Comp.CurrentPhase = phase;
            break;
        }
    }

    /// <summary>
    /// Scales all phases by one modifier. Doesn't update current phase.
    /// </summary>
    [PublicAPI]
    public void ScaleAllPhaseThresholds(Entity<MobPhasesComponent?> ent, float scale)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var thresholds = new Dictionary<FixedPoint2, int>(ent.Comp.PhaseThresholds.Reverse());
        foreach (var (damageThreshold, state) in thresholds)
        {
            // State stays the same, damage threshold is scaled.
            ent.Comp.PhaseThresholds.Remove(damageThreshold);
            ent.Comp.PhaseThresholds.Add(damageThreshold * scale, state);
        }
    }

    /// <summary>
    /// Sets phase thresholds back to default that were set on MapInit. Doesn't update current phase.
    /// </summary>
    [PublicAPI]
    public void UnscaleAllPhaseThresholds(Entity<MobPhasesComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.PhaseThresholds = ent.Comp.BasePhaseThresholds;
    }

    [PublicAPI]
    public void SetPhaseThreshold(Entity<MobPhasesComponent?> ent, FixedPoint2 damage, int phase)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var thresholds = new Dictionary<FixedPoint2, int>(ent.Comp.PhaseThresholds);
        foreach (var (damageThreshold, state) in thresholds)
        {
            if (state != phase)
                continue;
            ent.Comp.PhaseThresholds.Remove(damageThreshold);
        }
        ent.Comp.PhaseThresholds[damage] = phase;
    }
}

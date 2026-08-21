using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Body.Systems;
using Content.Shared._Onyx.Wounds;

namespace Content.Shared.EntityEffects.Effects.Damage;

public sealed partial class HealthChangeEntityEffectSystem
{
    [Dependency] private WoundDamageRoutingSystem _woundRouting = default!;

    private void ApplyTreatment(Entity<DamageableComponent> entity, EntityEffectEvent<HealthChange> args)
    {
        var change = new DamageSpecifier(args.Effect.Damage) * args.Scale;
        ApplyScoped(entity, change.DamageDict.Values.Any(amount => amount < 0),
            args.Effect.TreatmentCapabilities,
            () => _damageable.TryChangeDamage(entity.AsNullable(), change, args.Effect.IgnoreResistances,
                interruptsDoAfters: false));
    }

    private void ApplyScoped(Entity<DamageableComponent> entity, bool healing,
        IReadOnlySet<TreatmentCapability> capabilities, Action apply)
    {
        if (healing && HasComp<WoundHostComponent>(entity))
            _woundRouting.WithTreatmentCapabilities(entity, capabilities, apply);
        else
            apply();
    }
}

public sealed partial class EvenHealthChangeEntityEffectSystem
{
    [Dependency] private WoundDamageRoutingSystem _woundRouting = default!;

    private void ApplyTreatment(Entity<DamageableComponent> entity, EntityEffectEvent<EvenHealthChange> args)
    {
        void Apply()
        {
            foreach (var (group, amount) in args.Effect.Damage)
                _damageable.HealEvenly(entity.AsNullable(), amount * args.Scale, group);
        }

        if (HasComp<WoundHostComponent>(entity) && args.Effect.Damage.Values.Any(amount => amount < 0))
            _woundRouting.WithTreatmentCapabilities(entity, args.Effect.TreatmentCapabilities, Apply);
        else
            Apply();
    }
}

public sealed partial class DistributedHealthChangeEntityEffectSystem
{
    [Dependency] private WoundDamageRoutingSystem _woundRouting = default!;

    private void ApplyTreatment(Entity<DamageableComponent> entity, EntityEffectEvent<DistributedHealthChange> args)
    {
        void Apply()
        {
            foreach (var (group, amount) in args.Effect.Damage)
                _damageable.HealDistributed(entity.AsNullable(), amount * args.Scale, group);
        }

        if (HasComp<WoundHostComponent>(entity) && args.Effect.Damage.Values.Any(amount => amount < 0))
            _woundRouting.WithTreatmentCapabilities(entity, args.Effect.TreatmentCapabilities, Apply);
        else
            Apply();
    }
}

public sealed partial class MendFracturesEntityEffectSystem
    : EntityEffectSystem<WoundHostComponent, MendFractures>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private WoundSystem _wounds = default!;

    protected override void Effect(Entity<WoundHostComponent> entity,
        ref EntityEffectEvent<MendFractures> args)
    {
        var effect = args.Effect;
        var amount = effect.Amount * args.Scale;
        foreach (var (part, _) in _body.GetBodyChildren(entity))
        {
            if (_fractures.GetFracture(part) is not { } fracture ||
                (effect.Wounds.Count != 0 && !effect.Wounds.Contains(fracture.Comp1.Prototype)) ||
                fracture.Comp2.Grade < effect.MinimumGrade ||
                fracture.Comp2.Grade > effect.MaximumGrade)
                continue;

            _wounds.ChangeSeverity(fracture.Owner, -amount);
        }
    }
}

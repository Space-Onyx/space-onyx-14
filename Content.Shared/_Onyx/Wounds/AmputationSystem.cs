using System.Numerics;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Throwing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class AmputationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundableComponent, PartDamageOverflowedEvent>(OnPartDamageOverflowed);
    }

    private void OnPartDamageOverflowed(Entity<WoundableComponent> part, ref PartDamageOverflowedEvent args)
    {
        if (!_net.IsServer ||
            !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType == BodyPartType.Chest ||
            bodyPart.MaxDamage <= FixedPoint2.Zero)
            return;

        if (args.ExplosionAmputationCandidate && TryExplosionAmputate(args.Body, part, bodyPart, args.Damage))
            return;

        if (!part.Comp.Severable)
        {
            if (part.Comp.AmputationOverflow >= bodyPart.MaxDamage)
            {
                SetSeverable(part, true);
            }
            return;
        }

        if (IsFinishingHit(args.Body, bodyPart, args.Damage))
            TryAmputate(args.Body, part.Owner);
    }

    public void ApplyAmputationConsequences(EntityUid body, EntityUid parent)
    {
        if (!_net.IsServer || !HasComp<WoundableComponent>(parent) ||
            !TryComp(parent, out BodyPartComponent? parentPart))
            return;

        if (!TryComp(body, out WoundHostComponent? host))
            return;

        _wounds.CreateOrMergeWound(parent, host.AmputationConsequenceWound, parentPart.AmputationConsequenceSeverity);
    }

    public void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType is BodyPartType.Chest || bodyPart.Parent == null ||
            bodyPart.AmputationThresholds.Count == 0 ||
            !TryComp(part, out DamageableComponent? damageable))
            return;

        var damage = _damageable.GetAllDamage((part.Owner, damageable));
        if (args.ExplosionAmputationCandidate && TryExplosionAmputate(args.Body, part, bodyPart, args.Damage, damage))
            return;

        if (!part.Comp.Severable)
        {
            if (ReachedThreshold(damage, bodyPart.AmputationThresholds))
            {
                SetSeverable(part, true);
            }
            return;
        }

        if (GetThresholdProgress(damage, bodyPart.AmputationThresholds) < GetResetRatio(args.Body))
        {
            SetSeverable(part, false);
            return;
        }

        var damageBeforeHit = damage.Clone();
        foreach (var (type, amount) in args.Damage.DamageDict)
            if (amount > FixedPoint2.Zero)
                damageBeforeHit.DamageDict[type] = FixedPoint2.Max(FixedPoint2.Zero,
                    damageBeforeHit.DamageDict.GetValueOrDefault(type) - amount);

        if (ReachedThreshold(damageBeforeHit, bodyPart.AmputationThresholds) &&
            IsFinishingHit(args.Body, bodyPart, args.Damage))
            TryAmputate(args.Body, part.Owner);
    }

    /// <summary>
    /// Detaches the part deterministically and applies amputation consequences.
    /// </summary>
    public bool TryAmputate(EntityUid body, EntityUid part)
    {
        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType == BodyPartType.Chest)
            return false;

        var parent = bodyPart.Parent ?? part;
        if (!_body.TryDetachPart(part))
            return false;

        if (TryComp(body, out WoundHostComponent? host))
            _wounds.CreateOrMergeWound(parent, host.DismembermentWound,
                bodyPart.DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType));
        ApplyAmputationConsequences(body, parent);
        _throwing.TryThrow(part, Vector2.UnitY, baseThrowSpeed: 3f,
            pushbackRatio: 0f, doSpin: true);
        return true;
    }

    private static bool ReachedThreshold(
        DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> thresholds)
    {
        return GetThresholdProgress(damage, thresholds) >= 1f;
    }

    private static float GetThresholdProgress(
        DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> thresholds)
    {
        var progress = 0f;
        foreach (var (type, threshold) in thresholds)
        {
            if (threshold <= FixedPoint2.Zero)
                continue;
            progress += (float) damage.DamageDict.GetValueOrDefault(type) / (float) threshold;
        }
        return progress;
    }

    private bool IsFinishingHit(EntityUid body, BodyPartComponent part, DamageSpecifier damage)
    {
        if (!TryComp(body, out WoundHostComponent? host))
            return false;

        foreach (var (type, amount) in damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero || !part.AmputationThresholds.ContainsKey(type))
                continue;

            var minimum = part.DismembermentFinishingDamage.GetValueOrDefault(type,
                host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type));
            if (minimum > FixedPoint2.Zero && amount >= minimum)
                return true;
        }

        return false;
    }

    private bool TryExplosionAmputate(
        EntityUid body,
        Entity<WoundableComponent> part,
        BodyPartComponent bodyPart,
        DamageSpecifier hit,
        DamageSpecifier? totalDamage = null)
    {
        if (!IsFinishingHit(body, bodyPart, hit))
            return false;

        if (totalDamage == null)
        {
            totalDamage = _damageable.GetAllDamage(part.Owner).Clone();
            foreach (var (type, amount) in hit.DamageDict)
                if (amount > FixedPoint2.Zero)
                    totalDamage.DamageDict[type] = totalDamage.DamageDict.GetValueOrDefault(type) + amount;
        }

        var chance = Math.Clamp(GetThresholdProgress(totalDamage, bodyPart.AmputationThresholds) * 0.5f, 0f, 1f);
        return chance > 0f && _random.Prob(chance) && TryAmputate(body, part.Owner);
    }

    private float GetResetRatio(EntityUid body)
    {
        return TryComp(body, out WoundHostComponent? host)
            ? Math.Clamp(host.SeverableResetRatio, 0f, 1f)
            : 0.8f;
    }

    private void SetSeverable(Entity<WoundableComponent> part, bool value)
    {
        if (part.Comp.Severable == value)
            return;

        part.Comp.Severable = value;
        Dirty(part);
    }

    private static FixedPoint2 GetDismembermentSeverity(WoundHostComponent host, BodyPartType type)
    {
        return host.DismembermentSeverities.GetValueOrDefault(type, host.DefaultDismembermentSeverity);
    }
}

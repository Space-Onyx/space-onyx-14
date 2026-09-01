using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared.Bed.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Medical;
using Content.Shared._Onyx.Targeting;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundDamageRoutingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private WoundDamageProjectionSystem _projection = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TargetResolverSystem _targetResolver = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly HashSet<EntityUid> _routing = new();
    private readonly Dictionary<EntityUid, EntityUid> _requestedParts = new();
    private readonly HashSet<EntityUid> _applied = new();
    private readonly HashSet<EntityUid> _skipWoundHealing = new();
    private readonly Dictionary<EntityUid, IReadOnlySet<TreatmentCapability>> _treatmentCapabilities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<WoundHostComponent, DamageDealtEvent>(OnDamageDealt, before: [typeof(DamageableSystem)]);
        SubscribeLocalEvent<WoundableComponent, BeforeDamageChangedEvent>(OnBeforePartDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<WoundHostComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_net.IsServer || _routing.Contains(ent))
            return;

        args.Cancelled = true;
        RouteThroughBodyModifiers(ent, args.Damage, args.Origin);
    }

    private void OnDamageDealt(Entity<WoundHostComponent> ent, ref DamageDealtEvent args)
    {
        if (!_net.IsServer || !_routing.Contains(ent))
            return;

        var damage = args.Damage.Clone();
        args.Damage.DamageDict.Clear();
        RouteAppliedDamage(ent, damage, args.Origin, args.InterruptsDoAfters);
    }

    public void WithTreatmentCapabilities(EntityUid body, IReadOnlySet<TreatmentCapability> capabilities, Action action)
    {
        _treatmentCapabilities[body] = capabilities;
        try
        {
            action();
        }
        finally
        {
            _treatmentCapabilities.Remove(body);
        }
    }

    private void OnBeforePartDamageChanged(Entity<WoundableComponent> part, ref BeforeDamageChangedEvent args)
    {
        var filtered = FilterPartDamage(part, args.Damage, args.Origin);
        if (ReferenceEquals(filtered, args.Damage))
            return;

        args.Damage.DamageDict.Clear();
        foreach (var (type, amount) in filtered.DamageDict)
            args.Damage.DamageDict[type] = amount;
    }

    public bool TryApplyDamage(
        EntityUid body,
        DamageSpecifier damage,
        EntityUid? origin = null,
        EntityUid? requestedPart = null,
        bool ignoreResistances = false,
        bool healWounds = true)
    {
        if (!TryComp(body, out WoundHostComponent? host) || !_net.IsServer || _routing.Contains(body))
            return false;

        if (requestedPart is { } part)
        {
            if (!IsAttachedWoundablePart(body, part))
                return false;
            _requestedParts[body] = part;
        }

        try
        {
            if (!healWounds)
                _skipWoundHealing.Add(body);
            return RouteThroughBodyModifiers((body, host), damage, origin, ignoreResistances);
        }
        finally
        {
            _skipWoundHealing.Remove(body);
            _requestedParts.Remove(body);
        }
    }

    public bool TryApplyPartDamage(
        EntityUid body,
        EntityUid part,
        DamageSpecifier damage,
        EntityUid? origin = null,
        bool ignoreResistances = false,
        bool healWounds = true)
    {
        return TryApplyDamage(body, damage, origin, part, ignoreResistances, healWounds);
    }

    public bool TryApplyDistributedDamage(
        EntityUid body,
        DamageSpecifier damage,
        TargetBodyPart mask,
        DamageDistribution mode,
        EntityUid? origin = null,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true)
    {
        if (!TryComp(body, out WoundHostComponent? host) || !_net.IsServer || _routing.Contains(body) ||
            mode is not DamageDistribution.SplitEvenly and not DamageDistribution.SplitByPartWeight)
            return false;

        var systemic = new DamageSpecifier();
        var localized = new DamageSpecifier();
        foreach (var (type, amount) in damage.DamageDict)
            (host.LocalizedDamageTypes.Contains(type) ? localized : systemic).DamageDict[type] = amount;

        var parts = _targetResolver.GetMatchingParts(body, mask);
        parts.RemoveAll(part => !IsAttachedWoundablePart(body, part));
        var applied = false;

        if (!systemic.Empty)
            applied |= RouteThroughBodyModifiers((body, host), systemic, origin, ignoreResistances, interruptsDoAfters);

        if (localized.Empty || parts.Count == 0)
            return applied;

        var weights = new float[parts.Count];
        var totalWeight = 0f;
        for (var i = 0; i < parts.Count; i++)
        {
            var weight = 1f;
            if (mode == DamageDistribution.SplitByPartWeight && TryComp(parts[i], out BodyPartComponent? part))
                weight = host.TargetWeights.GetValueOrDefault(part.PartType, 1f);
            if (!float.IsFinite(weight) || weight <= 0f)
                weight = 1f;
            weights[i] = weight;
            totalWeight += weight;
        }

        var shares = new DamageSpecifier[parts.Count];
        for (var i = 0; i < shares.Length; i++)
            shares[i] = new DamageSpecifier();

        foreach (var (type, amount) in localized.DamageDict)
        {
            var remaining = amount.Value;
            for (var i = 0; i < parts.Count; i++)
            {
                var value = i == parts.Count - 1
                    ? remaining
                    : (int) ((long) amount.Value * weights[i] / totalWeight);
                shares[i].DamageDict[type] = FixedPoint2.FromHundredths(value);
                remaining -= value;
            }
        }

        for (var i = 0; i < parts.Count; i++)
        {
            _requestedParts[body] = parts[i];
            try
            {
                applied |= RouteThroughBodyModifiers((body, host), shares[i], origin, ignoreResistances, interruptsDoAfters);
            }
            finally
            {
                _requestedParts.Remove(body);
            }
        }

        return applied;
    }

    public bool TryApplyTargetedDamage(
        EntityUid body,
        DamageSpecifier damage,
        TargetBodyPart requested,
        EntityUid? shooter,
        out DamageSpecifier damageDealt,
        bool ignoreResistances = false)
    {
        return TryApplyTargetedDamage(body, damage, requested, shooter, out damageDealt, ignoreResistances, shooter);
    }

    public bool TryApplyCarrierDamage(
        EntityUid body,
        EntityUid carrier,
        DamageSpecifier damage,
        EntityUid? origin,
        out DamageSpecifier damageDealt,
        bool ignoreResistances = false)
    {
        damageDealt = new DamageSpecifier();
        return TryComp(carrier, out TargetingSnapshotComponent? snapshot) &&
               TryApplyTargetedDamage(body,
                   damage,
                   snapshot.RequestedTarget,
                   snapshot.Shooter,
                   out damageDealt,
                   ignoreResistances,
                   origin);
    }

    private bool TryApplyTargetedDamage(
        EntityUid body,
        DamageSpecifier damage,
        TargetBodyPart requested,
        EntityUid? shooter,
        out DamageSpecifier damageDealt,
        bool ignoreResistances,
        EntityUid? origin)
    {
        damageDealt = new DamageSpecifier();
        if (!TryComp(body, out WoundHostComponent? host) || !_net.IsServer || _routing.Contains(body) ||
            !_targetResolver.TryResolve(body, requested, shooter, out var part))
            return false;

        if (!TryComp(part, out DamageableComponent? partDamageable))
            return false;

        var before = _damage.GetPositiveDamage((part, partDamageable));
        var systemicBefore = CompOrNull<SystemicDamageComponent>(body)?.Damage.Clone() ?? new DamageSpecifier();
        _requestedParts[body] = part;
        try
        {
            if (!RouteThroughBodyModifiers((body, host), damage, origin, ignoreResistances))
                return false;

            var after = _damage.GetPositiveDamage((part, partDamageable));
            foreach (var (type, oldAmount) in before.DamageDict)
            {
                var delta = after.DamageDict.GetValueOrDefault(type) - oldAmount;
                if (delta != FixedPoint2.Zero)
                    damageDealt.DamageDict[type] = delta;
            }

            foreach (var (type, newAmount) in after.DamageDict)
            {
                if (!before.DamageDict.ContainsKey(type) && newAmount != FixedPoint2.Zero)
                    damageDealt.DamageDict[type] = newAmount;
            }

            var systemicAfter = CompOrNull<SystemicDamageComponent>(body)?.Damage;
            if (systemicAfter != null)
            {
                foreach (var type in systemicBefore.DamageDict.Keys.Concat(systemicAfter.DamageDict.Keys).Distinct())
                {
                    var delta = systemicAfter.DamageDict.GetValueOrDefault(type) -
                                systemicBefore.DamageDict.GetValueOrDefault(type);
                    if (delta != FixedPoint2.Zero)
                        damageDealt.DamageDict[type] = damageDealt.DamageDict.GetValueOrDefault(type) + delta;
                }
            }
            return true;
        }
        finally
        {
            _requestedParts.Remove(body);
        }
    }

    public EntityUid? ResolveDamagePart(EntityUid body, EntityUid? requestedPart)
    {
        if (requestedPart is { } requested)
            return IsAttachedWoundablePart(body, requested) ? requested : null;

        if (!TryComp(body, out WoundHostComponent? host))
            return null;

        var candidates = new List<(EntityUid Part, float Weight)>();
        var totalWeight = 0f;
        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            if (!HasComp<WoundableComponent>(part))
                continue;

            var weight = host.TargetWeights.GetValueOrDefault(component.PartType, 1f);
            if (weight <= 0f)
                continue;

            candidates.Add((part, weight));
            totalWeight += weight;
        }

        if (candidates.Count == 0)
            return null;

        var roll = _random.NextFloat() * totalWeight;
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f)
                return candidate.Part;
        }

        return candidates[^1].Part;
    }

    private bool TryGetActiveHandPart(EntityUid body, out EntityUid handPart)
    {
        handPart = EntityUid.Invalid;
        if (!TryComp(body, out HandsComponent? hands) ||
            _hands.GetActiveHand((body, hands)) is not { } activeHand ||
            !_hands.TryGetHand((body, hands), activeHand, out var hand))
            return false;

        var symmetry = hand.Value.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
        if (symmetry == BodyPartSymmetry.None)
            return false;

        foreach (var candidate in _body.GetBodyChildrenOfType(body, BodyPartType.Hand))
        {
            if (candidate.Component.Symmetry != symmetry || !HasComp<WoundableComponent>(candidate.Id))
                continue;

            handPart = candidate.Id;
            return true;
        }

        return false;
    }

    private bool RouteThroughBodyModifiers(
        Entity<WoundHostComponent> body,
        DamageSpecifier damage,
        EntityUid? origin,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true)
    {
        if (!_routing.Add(body))
            return false;

        var hadRequestedPart = _requestedParts.ContainsKey(body);
        try
        {
            if (!hadRequestedPart)
            {
                var localized = new DamageSpecifier();
                var localizedDamage = false;
                foreach (var (type, amount) in damage.DamageDict)
                {
                    if (body.Comp.LocalizedDamageTypes.Contains(type))
                    {
                        localized.DamageDict[type] = amount;
                        if (amount > FixedPoint2.Zero)
                            localizedDamage = true;
                    }
                }

                if (!localized.Empty && localizedDamage)
                {
                    if (origin is { } source &&
                        TryComp(source, out TargetingSnapshotComponent? snapshot) &&
                        _targetResolver.TryResolve(body, snapshot.RequestedTarget, snapshot.Shooter, out var snapshotPart))
                        _requestedParts[body] = snapshotPart;
                    else if (origin is { } light && HasComp<PoweredLightComponent>(light) &&
                             TryGetActiveHandPart(body, out var handPart))
                        _requestedParts[body] = handPart;
                    else if (origin is { } targetingSource && _targetResolver.TryResolve(body, targetingSource, out var targetedPart))
                        _requestedParts[body] = targetedPart;
                    else if (origin is { } defibrillator && HasComp<DefibrillatorComponent>(defibrillator) &&
                             _targetResolver.TryResolveAvailable(body, TargetBodyPart.Chest, out var chestPart))
                        _requestedParts[body] = chestPart;
                    else if (ResolveDamagePart(body, null) is { } randomPart)
                        _requestedParts[body] = randomPart;
                }
            }

            _applied.Remove(body);
            _damage.ChangeDamage(body.Owner, damage, ignoreResistances, interruptsDoAfters, origin);
            return _applied.Remove(body);
        }
        finally
        {
            if (!hadRequestedPart)
                _requestedParts.Remove(body);
            _routing.Remove(body);
        }
    }

    private void RouteAppliedDamage(Entity<WoundHostComponent> body, DamageSpecifier damage, EntityUid? origin, bool interruptsDoAfters)
    {
        var systemic = new DamageSpecifier();
        var localized = new DamageSpecifier();
        foreach (var (type, amount) in damage.DamageDict)
            (body.Comp.LocalizedDamageTypes.Contains(type) ? localized : systemic).DamageDict[type] = amount;

        if (!systemic.Empty)
        {
            if (ApplySystemicDamage(body, systemic))
                _applied.Add(body);
        }

        var healing = new DamageSpecifier();
        foreach (var (type, amount) in localized.DamageDict.ToArray())
        {
            if (amount >= FixedPoint2.Zero)
                continue;

            healing.DamageDict[type] = amount;
            localized.DamageDict.Remove(type);
        }

        if (!healing.Empty && ApplyLocalizedHealing(body, healing, origin, interruptsDoAfters))
            _applied.Add(body);

        EntityUid? part = _requestedParts.TryGetValue(body, out var requestedPart) ? requestedPart : null;
        if (part is null && !localized.Empty)
            part = ResolveDamagePart(body, null);

        if (!localized.Empty && part is { } target)
        {
            if (!TryComp(target, out BodyPartComponent? partComponent))
                return;

            localized = FilterPartDamage(target, localized);
            if (localized.Empty)
            {
                _projection.RefreshBodyDamage(body);
                return;
            }

            var modify = new PartDamageModifyEvent(
                body,
                target,
                partComponent.PartType,
                partComponent.Symmetry,
                localized);
            if (TryComp(body, out InventoryComponent? inventory))
                _inventory.RelayEvent((body, inventory), modify);

            localized = modify.Damage;
            if (localized.Empty)
            {
                _projection.RefreshBodyDamage(body);
                return;
            }

            var overflow = AccumulateAmputationOverflow(target, ref localized);
            if (!overflow.Empty)
            {
                var overflowed = new PartDamageOverflowedEvent(body, target, overflow);
                RaiseLocalEvent(target, ref overflowed);
            }

            if (localized.Empty || !_body.BodyHasChild(body, target))
            {
                _projection.RefreshBodyDamage(body);
                return;
            }

            if (_damage.TryChangeDamage(target,
                    localized,
                    out var appliedDamage,
                    ignoreResistances: true,
                    interruptsDoAfters: interruptsDoAfters,
                    origin: origin,
                    ignoreGlobalModifiers: true))
            {
                _applied.Add(body);
                var applied = new PartDamageAppliedEvent(body, target, appliedDamage,
                    !_skipWoundHealing.Contains(body), origin);
                RaiseLocalEvent(target, ref applied);
                return;
            }
        }

        _projection.RefreshBodyDamage(body);
    }

    private DamageSpecifier AccumulateAmputationOverflow(EntityUid part, ref DamageSpecifier damage)
    {
        var overflow = new DamageSpecifier();
        if (!TryComp(part, out WoundableComponent? woundable) ||
            !TryComp(part, out BodyPartComponent? bodyPart) ||
            bodyPart.MaxDamage <= FixedPoint2.Zero ||
            !TryComp(part, out DamageableComponent? damageable))
            return overflow;

        var current = _damage.GetPositiveDamage((part, damageable)).GetTotal();
        var remaining = bodyPart.MaxDamage - current;

        if (remaining > FixedPoint2.Zero && woundable.AmputationOverflow != FixedPoint2.Zero)
        {
            woundable.AmputationOverflow = FixedPoint2.Zero;
            Dirty(part, woundable);
        }

        if (remaining <= FixedPoint2.Zero)
        {
            foreach (var (type, amount) in damage.DamageDict.ToArray())
            {
                if (amount <= FixedPoint2.Zero)
                    continue;

                overflow.DamageDict[type] = amount;
                damage.DamageDict.Remove(type);
            }
        }
        else
        {
            foreach (var (type, amount) in damage.DamageDict.ToArray())
            {
                if (amount <= FixedPoint2.Zero)
                    continue;

                var fits = FixedPoint2.Min(amount, remaining);
                if (fits == amount)
                {
                    remaining -= fits;
                    continue;
                }

                damage.DamageDict[type] = fits;
                overflow.DamageDict[type] = amount - fits;
                remaining = FixedPoint2.Zero;
            }
        }

        if (overflow.Empty)
            return overflow;

        woundable.AmputationOverflow += overflow.GetTotal();
        Dirty(part, woundable);
        return overflow;
    }

    private bool ApplyLocalizedHealing(
        Entity<WoundHostComponent> body,
        DamageSpecifier healing,
        EntityUid? origin,
        bool interruptsDoAfters)
    {
        if (_requestedParts.TryGetValue(body, out var requestedPart))
            return CanTreatPart(body, requestedPart) &&
                   ApplyPartChange(body, requestedPart, healing, origin, interruptsDoAfters);

        var parts = new List<(EntityUid Part, DamageableComponent Damageable)>();
        foreach (var (part, _) in _body.GetBodyChildren(body))
            if (HasComp<WoundableComponent>(part) && CanTreatPart(body, part) &&
                TryComp(part, out DamageableComponent? damageable))
                parts.Add((part, damageable));

        var changes = parts.ToDictionary(part => part.Part, _ => new DamageSpecifier());
        foreach (var (type, amount) in healing.DamageDict)
        {
            var totalDamage = FixedPoint2.Zero;
            foreach (var part in parts)
            {
                if (CanPartReceiveDamage(part.Part, type))
                    totalDamage += _damage.GetPositiveDamage((part.Part, part.Damageable)).DamageDict.GetValueOrDefault(type);
            }

            if (totalDamage <= FixedPoint2.Zero)
                continue;

            var remaining = amount.Value;
            var damaged = parts.Where(part =>
                    CanPartReceiveDamage(part.Part, type) &&
                    _damage.GetPositiveDamage((part.Part, part.Damageable)).DamageDict.GetValueOrDefault(type) > FixedPoint2.Zero)
                .ToArray();
            for (var i = 0; i < damaged.Length; i++)
            {
                var partDamage = _damage.GetPositiveDamage((damaged[i].Part, damaged[i].Damageable))
                    .DamageDict.GetValueOrDefault(type);
                var value = i == damaged.Length - 1
                    ? remaining
                    : (int) ((long) amount.Value * partDamage.Value / totalDamage.Value);
                changes[damaged[i].Part].DamageDict[type] = FixedPoint2.FromHundredths(value);
                remaining -= value;
            }
        }

        var applied = false;
        foreach (var (part, change) in changes)
            if (!change.Empty)
                applied |= ApplyPartChange(body, part, change, origin, interruptsDoAfters);
        return applied;
    }

    private bool CanTreatPart(EntityUid body, EntityUid part)
    {
        if (!_treatmentCapabilities.TryGetValue(body, out var capabilities))
            return true;

        return TryComp(part, out WoundableComponent? woundable) &&
               _prototypes.TryIndex(woundable.Profile, out var profile) &&
               profile.TreatmentCapabilities.Overlaps(capabilities);
    }

    private bool ApplyPartChange(
        Entity<WoundHostComponent> body,
        EntityUid part,
        DamageSpecifier change,
        EntityUid? origin,
        bool interruptsDoAfters)
    {
        change = FilterPartDamage(part, change);
        if (change.Empty)
            return false;

        if (!_damage.TryChangeDamage(part,
                change,
                out var appliedDamage,
                ignoreResistances: true,
                interruptsDoAfters: interruptsDoAfters,
                origin: origin,
                ignoreGlobalModifiers: true))
            return false;

        var applied = new PartDamageAppliedEvent(body, part, appliedDamage,
            !_skipWoundHealing.Contains(body), origin);
        RaiseLocalEvent(part, ref applied);
        return true;
    }

    private DamageSpecifier FilterPartDamage(EntityUid part, DamageSpecifier damage, EntityUid? origin = null)
    {
        if (!TryComp(part, out WoundableComponent? woundable) ||
            !_prototypes.TryIndex(woundable.Profile, out var profile))
            return damage;

        var filtered = damage.Clone();
        foreach (var type in filtered.DamageDict.Keys.ToArray())
        {
            if (profile.AcceptedDamageTypes.Count != 0 && !profile.AcceptedDamageTypes.Contains(type))
                filtered.DamageDict.Remove(type);
        }

        var recoveryMultiplier = 1f;
        if (origin is { } source && HasComp<PassiveDamageComponent>(source))
            recoveryMultiplier = profile.PassiveRecoveryMultiplier;
        else if (origin is { } bed && HasComp<HealOnBuckleComponent>(bed))
            recoveryMultiplier = profile.BedRecoveryMultiplier;

        if (!float.IsFinite(recoveryMultiplier))
            recoveryMultiplier = 1f;
        recoveryMultiplier = Math.Max(0f, recoveryMultiplier);
        if (recoveryMultiplier != 1f)
        {
            foreach (var (type, amount) in filtered.DamageDict.ToArray())
            {
                if (amount < FixedPoint2.Zero)
                    filtered.DamageDict[type] = amount * recoveryMultiplier;
            }
        }

        return filtered;
    }

    private bool CanPartReceiveDamage(EntityUid part, ProtoId<DamageTypePrototype> type)
    {
        return !TryComp(part, out WoundableComponent? woundable) ||
               !_prototypes.TryIndex(woundable.Profile, out var profile) ||
               profile.AcceptedDamageTypes.Count == 0 ||
               profile.AcceptedDamageTypes.Contains(type);
    }

    private bool ApplySystemicDamage(EntityUid body, DamageSpecifier change)
    {
        var systemic = EnsureComp<SystemicDamageComponent>(body);
        var applied = new DamageSpecifier();
        var changed = false;
        foreach (var (type, amount) in change.DamageDict)
        {
            if (!_damage.CanBeDamagedBy(body, type))
                continue;

            var oldValue = systemic.Damage.DamageDict.GetValueOrDefault(type);
            var value = FixedPoint2.Max(FixedPoint2.Zero, systemic.Damage.DamageDict.GetValueOrDefault(type) + amount);
            if (value == oldValue)
                continue;

            changed = true;
            applied.DamageDict[type] = value - oldValue;
            if (value == FixedPoint2.Zero)
                systemic.Damage.DamageDict.Remove(type);
            else
                systemic.Damage.DamageDict[type] = value;
        }

        if (!changed)
            return false;

        Dirty(body, systemic);
        if (TryComp(body, out WoundHostComponent? host) &&
            _targetResolver.TryResolveExact(body, host.SystemicPainTarget, out var painTarget))
            _pain.ApplyDamage(painTarget, applied);
        return true;
    }

    private bool IsAttachedWoundablePart(EntityUid body, EntityUid part)
    {
        return HasComp<WoundableComponent>(part) && _body.BodyHasChild(body, part);
    }
}

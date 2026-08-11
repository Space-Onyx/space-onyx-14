using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared._Onyx.Targeting;
using Robust.Shared.Network;
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

    private readonly HashSet<EntityUid> _routing = new();
    private readonly Dictionary<EntityUid, EntityUid> _requestedParts = new();
    private readonly HashSet<EntityUid> _applied = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<WoundHostComponent, DamageDealtEvent>(OnDamageDealt, before: [typeof(DamageableSystem)]);
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

    public bool TryApplyDamage(EntityUid body, DamageSpecifier damage, EntityUid? origin = null, EntityUid? requestedPart = null)
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
            return RouteThroughBodyModifiers((body, host), damage, origin);
        }
        finally
        {
            _requestedParts.Remove(body);
        }
    }

    public bool TryApplyPartDamage(EntityUid body, EntityUid part, DamageSpecifier damage, EntityUid? origin = null)
    {
        return TryApplyDamage(body, damage, origin, part);
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
            return true;
        }
        finally
        {
            _requestedParts.Remove(body);
        }
    }

    public EntityUid? ResolveDamagePart(EntityUid body, EntityUid? requestedPart, DamageSpecifier damage)
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
                    else if (ResolveDamagePart(body, null, localized) is { } randomPart)
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
            ApplySystemicDamage(body, systemic);
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
            part = ResolveDamagePart(body, null, localized);

        if (!localized.Empty && part is { } target)
        {
            if (!TryComp(target, out BodyPartComponent? partComponent))
                return;

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

            if (_damage.TryChangeDamage(target,
                    localized,
                    out var appliedDamage,
                    ignoreResistances: true,
                    interruptsDoAfters: interruptsDoAfters,
                    origin: origin,
                    ignoreGlobalModifiers: true))
            {
                _applied.Add(body);
                var applied = new PartDamageAppliedEvent(body, target, appliedDamage);
                RaiseLocalEvent(target, ref applied);
                return;
            }
        }

        _projection.RefreshBodyDamage(body);
    }

    private bool ApplyLocalizedHealing(
        Entity<WoundHostComponent> body,
        DamageSpecifier healing,
        EntityUid? origin,
        bool interruptsDoAfters)
    {
        if (_requestedParts.TryGetValue(body, out var requestedPart))
            return ApplyPartChange(body, requestedPart, healing, origin, interruptsDoAfters);

        var parts = new List<(EntityUid Part, DamageableComponent Damageable)>();
        foreach (var (part, _) in _body.GetBodyChildren(body))
            if (HasComp<WoundableComponent>(part) && TryComp(part, out DamageableComponent? damageable))
                parts.Add((part, damageable));

        var changes = parts.ToDictionary(part => part.Part, _ => new DamageSpecifier());
        foreach (var (type, amount) in healing.DamageDict)
        {
            var totalDamage = FixedPoint2.Zero;
            foreach (var part in parts)
                totalDamage += _damage.GetPositiveDamage((part.Part, part.Damageable)).DamageDict.GetValueOrDefault(type);

            if (totalDamage <= FixedPoint2.Zero)
                continue;

            var remaining = amount.Value;
            var damaged = parts.Where(part =>
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

    private bool ApplyPartChange(
        Entity<WoundHostComponent> body,
        EntityUid part,
        DamageSpecifier change,
        EntityUid? origin,
        bool interruptsDoAfters)
    {
        if (!_damage.TryChangeDamage(part,
                change,
                out var appliedDamage,
                ignoreResistances: true,
                interruptsDoAfters: interruptsDoAfters,
                origin: origin,
                ignoreGlobalModifiers: true))
            return false;

        var applied = new PartDamageAppliedEvent(body, part, appliedDamage);
        RaiseLocalEvent(part, ref applied);
        return true;
    }

    private void ApplySystemicDamage(EntityUid body, DamageSpecifier change)
    {
        var systemic = EnsureComp<SystemicDamageComponent>(body);
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
            if (value == FixedPoint2.Zero)
                systemic.Damage.DamageDict.Remove(type);
            else
                systemic.Damage.DamageDict[type] = value;
        }

        if (changed)
            Dirty(body, systemic);
    }

    private bool IsAttachedWoundablePart(EntityUid body, EntityUid part)
    {
        return HasComp<WoundableComponent>(part) && _body.BodyHasChild(body, part);
    }
}

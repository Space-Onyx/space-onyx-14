using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Healing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundHealingSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundDamageRoutingSystem _routing = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, ResolveHealingPartEvent>(OnResolveHealingPart);
    }

    private void OnResolveHealingPart(Entity<WoundHostComponent> body, ref ResolveHealingPartEvent args)
    {
        args.Part = ResolveHealingPart(body, args.RequestedPart, args.Healing, args.DamageContainers,
            args.TreatmentCapabilities, args.AllowedWoundStages, args.BloodlossModifier, args.HealWounds);

        var hasLocalized = args.Healing.DamageDict.Any(entry => body.Comp.LocalizedDamageTypes.Contains(entry.Key));
        args.Accepted = args.Part != null || !hasLocalized;
    }

    public EntityUid? ResolveHealingPart(
        EntityUid body,
        EntityUid? requestedPart,
        DamageSpecifier healing,
        IReadOnlyList<ProtoId<DamageContainerPrototype>>? damageContainers,
        IReadOnlySet<TreatmentCapability> treatmentCapabilities,
        IReadOnlySet<string>? allowedWoundStages,
        float bloodlossModifier,
        bool healWounds = false)
    {
        if (!TryComp(body, out WoundHostComponent? host))
            return null;

        if (requestedPart is { } requested)
            return IsCompatiblePart(body, requested, damageContainers, treatmentCapabilities) ? requested : null;

        EntityUid? selected = null;
        var best = FixedPoint2.Zero;
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!IsCompatiblePart(body, part, damageContainers, treatmentCapabilities) ||
                !TryComp(part, out DamageableComponent? damageable))
                continue;

            var score = FixedPoint2.Zero;
            var partDamage = _damage.GetPositiveDamage((part, damageable)).DamageDict;
            foreach (var (type, amount) in healing.DamageDict)
            {
                if (amount >= FixedPoint2.Zero || !host.LocalizedDamageTypes.Contains(type))
                    continue;

                score += FixedPoint2.Min(-amount, partDamage.GetValueOrDefault(type));
            }
            if (healWounds)
                score += _wounds.GetHealingPotential(part, healing, allowedWoundStages);

            if (bloodlossModifier < 0)
                score += FixedPoint2.New(_bleeding.GetPartRate(part));

            if (score <= best)
                continue;

            best = score;
            selected = part;
        }

        return selected;
    }

    public bool CanTreatBleeding(EntityUid part) => _bleeding.GetPartRate(part) > 0f;

    public bool HasTreatableWounds(EntityUid part, DamageSpecifier healing, IReadOnlySet<string>? allowedStages) =>
        _wounds.GetHealingPotential(part, healing, allowedStages) > FixedPoint2.Zero;

    public bool TryApplyHealing(
        EntityUid body,
        EntityUid? requestedPart,
        Entity<HealingComponent> healing,
        EntityUid? origin,
        out DamageSpecifier healed,
        out bool stoppedBleeding)
    {
        healed = new DamageSpecifier();
        stoppedBleeding = false;
        if (!_net.IsServer || (!healing.Comp.HealDamage && !healing.Comp.HealWounds &&
                               healing.Comp.BloodlossModifier >= 0f && healing.Comp.ModifyBloodLevel == 0f) ||
            !TryComp(body, out WoundHostComponent? host) ||
            !TryComp(body, out DamageableComponent? bodyDamageable))
            return false;

        var resolve = new ResolveHealingPartEvent(body, healing.Comp.Damage, healing.Comp.DamageContainers,
            healing.Comp.TreatmentCapabilities, healing.Comp.AllowedWoundStages,
            healing.Comp.BloodlossModifier, requestedPart, healing.Comp.HealWounds);
        RaiseLocalEvent(body, ref resolve);
        if (!resolve.Accepted)
            return false;

        var change = healing.Comp.Damage * _damage.UniversalTopicalsHealModifier;
        var before = _damage.GetPositiveDamage((body, bodyDamageable));
        var applied = false;
        if (resolve.Part is { } part)
        {
            if (healing.Comp.HealDamage)
                applied = _routing.TryApplyPartDamage(body, part, change, origin, healWounds: healing.Comp.HealWounds);
            if (healing.Comp.HealWounds && !healing.Comp.HealDamage)
                applied |= _wounds.TryHealWounds(part, change, healing.Comp.AllowedWoundStages);
        }
        else if (healing.Comp.HealDamage)
            applied = _routing.TryApplyDamage(body, change, origin, healWounds: healing.Comp.HealWounds);

        if (healing.Comp.BloodlossModifier < 0 && resolve.Part is { } bleedingPart)
        {
            stoppedBleeding = TreatBleeding(bleedingPart, -healing.Comp.BloodlossModifier);
        }

        var after = _damage.GetPositiveDamage((body, bodyDamageable));
        foreach (var (type, amount) in before.DamageDict)
        {
            var delta = after.DamageDict.GetValueOrDefault(type) - amount;
            if (delta != FixedPoint2.Zero)
                healed.DamageDict[type] = delta;
        }

        return applied || stoppedBleeding || healing.Comp.ModifyBloodLevel != 0f;
    }

    private bool TreatBleeding(EntityUid part, float amount)
    {
        Entity<WoundComponent, WoundBleedingComponent>? selected = null;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (!TryComp(wound, out WoundBleedingComponent? bleeding) || bleeding.CurrentRate <= 0f ||
                selected is { } current && current.Comp2.CurrentRate >= bleeding.CurrentRate)
                continue;

            selected = (wound, wound.Comp, bleeding);
        }

        return selected is { } target && _bleeding.ReduceBleeding(target.Owner, FixedPoint2.New(amount));
    }

    public bool IsCompatiblePart(
        EntityUid body,
        EntityUid part,
        IReadOnlyList<ProtoId<DamageContainerPrototype>>? damageContainers,
        IReadOnlySet<TreatmentCapability> treatmentCapabilities)
    {
        if (!_body.BodyHasChild(body, part) || !TryComp(part, out WoundableComponent? woundable) ||
            !_prototypes.TryIndex(woundable.Profile, out var profile) ||
            !profile.TreatmentCapabilities.Overlaps(treatmentCapabilities))
            return false;

        return true;
    }

}

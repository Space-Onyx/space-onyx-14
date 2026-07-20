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
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, ResolveHealingPartEvent>(OnResolveHealingPart);
    }

    private void OnResolveHealingPart(Entity<WoundHostComponent> body, ref ResolveHealingPartEvent args)
    {
        args.Part = ResolveHealingPart(body, args.RequestedPart, args.Healing, args.DamageContainers,
            args.BloodlossModifier);

        var hasLocalized = args.Healing.DamageDict.Any(entry => body.Comp.LocalizedDamageTypes.Contains(entry.Key));
        args.Accepted = args.Part != null || !hasLocalized;
    }

    public EntityUid? ResolveHealingPart(
        EntityUid body,
        EntityUid? requestedPart,
        DamageSpecifier healing,
        IReadOnlyList<ProtoId<DamageContainerPrototype>>? damageContainers,
        float bloodlossModifier)
    {
        if (!TryComp(body, out WoundHostComponent? host))
            return null;

        if (requestedPart is { } requested)
            return IsCompatiblePart(body, requested, damageContainers) ? requested : null;

        EntityUid? selected = null;
        var best = FixedPoint2.Zero;
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!IsCompatiblePart(body, part, damageContainers) || !TryComp(part, out DamageableComponent? damageable))
                continue;

            var score = FixedPoint2.Zero;
            var partDamage = _damage.GetAllDamage((part, damageable)).DamageDict;
            foreach (var (type, amount) in healing.DamageDict)
            {
                if (amount >= FixedPoint2.Zero || !host.LocalizedDamageTypes.Contains(type))
                    continue;

                score += FixedPoint2.Min(-amount, partDamage.GetValueOrDefault(type));
            }

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
        if (!_net.IsServer || !TryComp(body, out WoundHostComponent? host))
            return false;

        var resolve = new ResolveHealingPartEvent(body, healing.Comp.Damage, healing.Comp.DamageContainers,
            healing.Comp.BloodlossModifier, requestedPart);
        RaiseLocalEvent(body, ref resolve);
        if (!resolve.Accepted)
            return false;

        var before = _damage.GetAllDamage(body).Clone();
        var change = healing.Comp.Damage * _damage.UniversalTopicalsHealModifier;
        var applied = resolve.Part is { } part
            ? _routing.TryApplyPartDamage(body, part, change, origin)
            : _routing.TryApplyDamage(body, change, origin);

        if (healing.Comp.BloodlossModifier < 0 && resolve.Part is { } bleedingPart)
        {
            var treatment = GetTreatment(healing.Comp.Damage);
            stoppedBleeding = _bleeding.TreatMostBleedingWound(bleedingPart, treatment);
        }

        var after = _damage.GetAllDamage(body);
        foreach (var (type, amount) in before.DamageDict)
        {
            var delta = after.DamageDict.GetValueOrDefault(type) - amount;
            if (delta != FixedPoint2.Zero)
                healed.DamageDict[type] = delta;
        }

        return applied || stoppedBleeding || healing.Comp.ModifyBloodLevel != 0f;
    }

    private bool IsCompatiblePart(
        EntityUid body,
        EntityUid part,
        IReadOnlyList<ProtoId<DamageContainerPrototype>>? damageContainers)
    {
        if (!_body.BodyHasChild(body, part) || !HasComp<WoundableComponent>(part) ||
            !TryComp(part, out InjurableComponent? injurable))
            return false;

        return damageContainers is null || injurable.DamageContainer is null ||
               damageContainers.Contains(injurable.DamageContainer.Value);
    }

    private static BleedingTreatment GetTreatment(DamageSpecifier healing)
    {
        if (healing.DamageDict.Values.Any(amount => amount > FixedPoint2.Zero))
            return BleedingTreatment.Clamped;

        var blunt = new ProtoId<DamageTypePrototype>("Blunt");
        var slash = new ProtoId<DamageTypePrototype>("Slash");
        var piercing = new ProtoId<DamageTypePrototype>("Piercing");
        return healing.DamageDict.GetValueOrDefault(blunt) < FixedPoint2.Zero &&
               healing.DamageDict.GetValueOrDefault(slash) < FixedPoint2.Zero &&
               healing.DamageDict.GetValueOrDefault(piercing) < FixedPoint2.Zero
            ? BleedingTreatment.Sutured
            : BleedingTreatment.Bandaged;
    }
}

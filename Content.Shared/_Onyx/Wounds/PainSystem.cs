using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    private const float HealingPainReduction = 0.8f;

    private float _recoveryAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        _recoveryAccumulator += frameTime;
        if (_recoveryAccumulator < 1f)
            return;

        var elapsed = _recoveryAccumulator;
        _recoveryAccumulator = 0f;
        var query = EntityQueryEnumerator<PainComponent>();
        while (query.MoveNext(out var uid, out var pain))
        {
            if (HasComp<BodyPartComponent>(uid))
                RecoverPain((uid, pain), elapsed);
            DecayPainSuppression((uid, pain), elapsed);
        }
    }

    public FixedPoint2 GetPain(Entity<PainComponent?> entity)
    {
        return Resolve(entity, ref entity.Comp, false)
            ? FixedPoint2.Max(FixedPoint2.Zero, entity.Comp.Value - entity.Comp.Suppression)
            : FixedPoint2.Zero;
    }

    public FixedPoint2 GetRawPain(Entity<PainComponent?> entity)
    {
        return Resolve(entity, ref entity.Comp, false) ? entity.Comp.Value : FixedPoint2.Zero;
    }

    public bool SetPain(Entity<PainComponent?> entity, FixedPoint2 value)
    {
        if (!_net.IsServer || !Resolve(entity, ref entity.Comp, false))
            return false;

        value = FixedPoint2.Max(FixedPoint2.Zero, value);
        var old = entity.Comp.Value;
        if (old == value)
            return false;

        entity.Comp.Value = value;
        Dirty(entity);

        var changed = new PainChangedEvent(entity, old, value);
        RaiseLocalEvent(entity, ref changed);

        if (TryComp(entity, out BodyPartComponent? part) && part.Body is { } body &&
            TryComp(body, out PainComponent? bodyPain))
            SetPain((body, bodyPain), bodyPain.Value + value - old);
        return true;
    }

    public bool ChangePain(Entity<PainComponent?> entity, FixedPoint2 delta)
    {
        if (!Resolve(entity, ref entity.Comp, false) || delta == FixedPoint2.Zero)
            return false;

        return SetPain(entity, entity.Comp.Value + delta);
    }

    public bool RecoverPain(Entity<PainComponent?> entity, float seconds)
    {
        if (!Resolve(entity, ref entity.Comp, false) || seconds <= 0f ||
            entity.Comp.Value <= FixedPoint2.Zero || entity.Comp.RecoveryPerSecond <= FixedPoint2.Zero)
            return false;

        return ChangePain(entity, -entity.Comp.RecoveryPerSecond * seconds);
    }

    public bool SuppressPain(Entity<PainComponent?> entity, string identifier, FixedPoint2 amount, TimeSpan decayDuration)
    {
        if (!_net.IsServer || string.IsNullOrWhiteSpace(identifier) || amount <= FixedPoint2.Zero ||
            decayDuration <= TimeSpan.Zero || !Resolve(entity, ref entity.Comp, false))
            return false;

        var accumulated = amount;
        if (entity.Comp.SuppressionModifiers.TryGetValue(identifier, out var current))
            accumulated += current.Amount;

        entity.Comp.SuppressionModifiers[identifier] = new PainSuppressionModifier(
            accumulated,
            accumulated / (float) decayDuration.TotalSeconds);
        RefreshSuppression((entity.Owner, entity.Comp));
        return true;
    }

    public bool ClearPainSuppression(Entity<PainComponent?> entity)
    {
        if (!_net.IsServer || !Resolve(entity, ref entity.Comp, false) ||
            entity.Comp.SuppressionModifiers.Count == 0 && entity.Comp.Suppression == FixedPoint2.Zero)
            return false;

        entity.Comp.SuppressionModifiers.Clear();
        entity.Comp.Suppression = FixedPoint2.Zero;
        Dirty(entity);
        return true;
    }

    public bool DecayPainSuppression(Entity<PainComponent?> entity, float seconds)
    {
        if (!_net.IsServer || seconds <= 0f || !Resolve(entity, ref entity.Comp, false) ||
            entity.Comp.SuppressionModifiers.Count == 0)
            return false;

        foreach (var (identifier, modifier) in entity.Comp.SuppressionModifiers.ToArray())
        {
            var amount = FixedPoint2.Max(FixedPoint2.Zero, modifier.Amount - modifier.DecayPerSecond * seconds);
            if (amount == FixedPoint2.Zero)
                entity.Comp.SuppressionModifiers.Remove(identifier);
            else
                entity.Comp.SuppressionModifiers[identifier] = modifier with { Amount = amount };
        }

        RefreshSuppression((entity.Owner, entity.Comp));
        return true;
    }

    public bool ApplyDamage(EntityUid part, DamageSpecifier delta, BodyPartComponent? bodyPart = null,
        PainComponent? pain = null)
    {
        if (!Resolve(part, ref bodyPart, ref pain, false))
            return false;

        var change = CalculatePain(delta, bodyPart.PartType, pain.DamageTypes) -
                     CalculatePain(delta * -1f, bodyPart.PartType, pain.DamageTypes) * HealingPainReduction;
        return change != FixedPoint2.Zero && ChangePain((part, pain), change);
    }

    public FixedPoint2 CalculatePain(DamageSpecifier damage, BodyPartType partType,
        IReadOnlySet<ProtoId<DamageTypePrototype>> painTypes)
    {
        var painfulDamage = FixedPoint2.Zero;
        foreach (var (type, amount) in damage.DamageDict)
            if (amount > FixedPoint2.Zero && painTypes.Contains(type))
                painfulDamage += amount;

        var multiplier = partType switch
        {
            BodyPartType.Head => 1.5f,
            BodyPartType.Hand or BodyPartType.Foot => 0.8f,
            BodyPartType.Arm or BodyPartType.Leg => 0.6f,
            BodyPartType.Tail => 0.5f,
            _ => 1f,
        };
        return FixedPoint2.Max(FixedPoint2.Zero, painfulDamage * multiplier);
    }

    private void RefreshSuppression(Entity<PainComponent> entity)
    {
        var value = FixedPoint2.Zero;
        foreach (var modifier in entity.Comp.SuppressionModifiers.Values)
            value += modifier.Amount;

        if (entity.Comp.Suppression == value)
            return;

        entity.Comp.Suppression = value;
        Dirty(entity);
    }
}

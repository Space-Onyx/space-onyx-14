using System.Linq;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId PainShockEffect = "StatusEffectPainShock";
    private static readonly FixedPoint2 PainShockThreshold = 130;
    private static readonly FixedPoint2 PainShockRecoveryThreshold = 115;
    private float _recoveryAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PainComponent, ComponentShutdown>(OnPainShutdown);
        SubscribeLocalEvent<PainComponent, RejuvenateEvent>(OnRejuvenate);
    }

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

        var suppressionQuery = EntityQueryEnumerator<PainComponent>();
        while (suppressionQuery.MoveNext(out var suppressionUid, out var suppressionPain))
            DecayPainSuppression((suppressionUid, suppressionPain), elapsed);

        var recoveryQuery = EntityQueryEnumerator<PainComponent>();
        while (recoveryQuery.MoveNext(out var partUid, out var partPain))
        {
            if (TryComp(partUid, out BodyPartComponent? part))
                RecoverPain((partUid, partPain), elapsed, part);
        }

        var bodyQuery = EntityQueryEnumerator<PainComponent, MobStateComponent, PainShockTargetComponent>();
        while (bodyQuery.MoveNext(out var bodyUid, out var bodyPain, out var mobState, out _))
        {
            UpdatePainShock((bodyUid, bodyPain), mobState);
        }
    }

    public FixedPoint2 GetPain(Entity<PainComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false) || IsPainNumb(entity.Owner))
            return FixedPoint2.Zero;

        var suppression = entity.Comp.Suppression;
        if (TryComp(entity, out BodyPartComponent? part) && part.Body is { } body &&
            TryComp(body, out PainComponent? bodyPain) && bodyPain.Value > FixedPoint2.Zero)
        {
            var share = entity.Comp.Value.Float() / bodyPain.Value.Float();
            suppression += bodyPain.Suppression * share;
        }

        return FixedPoint2.Max(FixedPoint2.Zero, entity.Comp.Value - suppression);
    }

    public FixedPoint2 GetRawPain(Entity<PainComponent?> entity)
    {
        return Resolve(entity, ref entity.Comp, false) ? entity.Comp.Value : FixedPoint2.Zero;
    }

    public bool SetPain(Entity<PainComponent?> entity, FixedPoint2 value)
    {
        if (!_net.IsServer || !Resolve(entity, ref entity.Comp, false))
            return false;

        value = FixedPoint2.Clamp(value, FixedPoint2.Zero, entity.Comp.SoftPainCap);
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

    public bool RecoverPain(Entity<PainComponent?> entity, float seconds, BodyPartComponent? part = null)
    {
        if (!Resolve(entity, ref entity.Comp, false) || seconds <= 0f ||
            entity.Comp.Value <= FixedPoint2.Zero || entity.Comp.RecoveryPerSecond <= FixedPoint2.Zero)
            return false;

        var minimum = FixedPoint2.Zero;
        if (TryComp(entity, out DamageableComponent? damageable))
            minimum = CalculatePain(_damage.GetPositiveDamage((entity, damageable)), entity.Comp.DamageMultipliers);

        var recovery = entity.Comp.RecoveryPerSecond;
        if (Resolve(entity, ref part, false) && part.Body is { } body &&
            TryComp(body, out PainComponent? bodyPain) && bodyPain.Value > FixedPoint2.Zero)
        {
            var suppressedPain = FixedPoint2.Min(bodyPain.Suppression, bodyPain.Value);
            var suppressionRatio = suppressedPain.Float() / bodyPain.Value.Float();
            recovery *= 1f + (GetRecoveryMultiplier(bodyPain) - 1f) * suppressionRatio;
        }

        var recovered = FixedPoint2.Max(minimum, entity.Comp.Value - recovery * seconds);
        return SetPain(entity, recovered);
    }

    private void UpdatePainShock(Entity<PainComponent> entity, MobStateComponent mobState)
    {
        var pain = GetPain((entity.Owner, entity.Comp));
        if (!HasComp<PainShockComponent>(entity))
        {
            if (mobState.CurrentState != MobState.Alive || pain < PainShockThreshold)
                return;

            var wasAlreadySleeping = HasComp<SleepingComponent>(entity);
            if (!_statusEffects.TrySetStatusEffectDuration(entity, PainShockEffect))
                return;

            EnsureComp<PainShockComponent>(entity).WasSleeping = wasAlreadySleeping;
            return;
        }

        if (mobState.CurrentState == MobState.Dead)
        {
            _statusEffects.TryRemoveStatusEffect(entity, PainShockEffect);
            RemComp<PainShockComponent>(entity);
            return;
        }

        if (mobState.CurrentState != MobState.Alive || pain > PainShockRecoveryThreshold)
        {
            if (!_statusEffects.HasStatusEffect(entity, PainShockEffect) &&
                !_statusEffects.TrySetStatusEffectDuration(entity, PainShockEffect))
                RemComp<PainShockComponent>(entity);
            return;
        }

        if (_statusEffects.HasStatusEffect(entity, PainShockEffect))
        {
            _statusEffects.TryRemoveStatusEffect(entity, PainShockEffect);
            return;
        }

        var wasSleepingBeforeShock = Comp<PainShockComponent>(entity).WasSleeping;
        RemComp<PainShockComponent>(entity);
        if (!wasSleepingBeforeShock && mobState.CurrentState == MobState.Alive)
            _sleeping.TryWaking(entity.Owner);
    }

    private void OnRejuvenate(Entity<PainComponent> entity, ref RejuvenateEvent args)
    {
        if (_net.IsServer)
            ClearPainShock(entity);
    }

    private void OnPainShutdown(Entity<PainComponent> entity, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            ClearPainShock(entity);
    }

    private void ClearPainShock(EntityUid entity)
    {
        _statusEffects.TryRemoveStatusEffect(entity, PainShockEffect);
        RemComp<PainShockComponent>(entity);
    }

    private bool IsPainNumb(EntityUid entity)
    {
        if (TryComp(entity, out BodyPartComponent? part) && part.Body is { } body)
            entity = body;

        return _statusEffects.EnumerateStatusEffects<PainNumbnessStatusEffectComponent>(entity)
            .Any(effect => effect.Comp1.Applied);
    }

    public bool SuppressPain(Entity<PainComponent?> entity, string identifier, FixedPoint2 amount,
        TimeSpan decayDuration, float recoveryMultiplier = 1f)
    {
        if (!_net.IsServer || string.IsNullOrWhiteSpace(identifier) || amount <= FixedPoint2.Zero ||
            decayDuration <= TimeSpan.Zero || !float.IsFinite(recoveryMultiplier) || recoveryMultiplier < 1f ||
            !Resolve(entity, ref entity.Comp, false))
            return false;

        var accumulated = amount;
        if (entity.Comp.SuppressionModifiers.TryGetValue(identifier, out var current))
        {
            accumulated += current.Amount;
            recoveryMultiplier = Math.Max(recoveryMultiplier, current.RecoveryMultiplier);
        }

        entity.Comp.SuppressionModifiers[identifier] = new PainSuppressionModifier(
            accumulated,
            accumulated / (float) decayDuration.TotalSeconds,
            recoveryMultiplier);
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

        var change = CalculatePain(delta, pain.DamageMultipliers);
        return change != FixedPoint2.Zero && ChangePain((part, pain), change);
    }

    public FixedPoint2 CalculatePain(DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, float> painMultipliers)
    {
        var painfulDamage = FixedPoint2.Zero;
        foreach (var (type, amount) in damage.DamageDict)
            if (amount > FixedPoint2.Zero && painMultipliers.TryGetValue(type, out var multiplier))
                painfulDamage += amount * multiplier;

        return FixedPoint2.Max(FixedPoint2.Zero, painfulDamage);
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

    private static float GetRecoveryMultiplier(PainComponent pain)
    {
        if (pain.Suppression <= FixedPoint2.Zero)
            return 1f;

        var weightedMultiplier = 0f;
        foreach (var modifier in pain.SuppressionModifiers.Values)
            weightedMultiplier += modifier.Amount.Float() * modifier.RecoveryMultiplier;

        return Math.Max(1f, weightedMultiplier / pain.Suppression.Float());
    }
}

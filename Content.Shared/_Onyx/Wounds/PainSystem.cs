using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
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
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly EntProtoId PainShockEffect = "StatusEffectPainShock";
    private static readonly FixedPoint2 PainShockThreshold = 130;
    private static readonly FixedPoint2 PainShockRearmThreshold = 110;
    private static readonly TimeSpan PainShockStunTime = TimeSpan.FromSeconds(2f);
    private float _recoveryAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PainComponent, ComponentShutdown>(OnPainShutdown);
        SubscribeLocalEvent<PainComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public void ApplyOneTimePain(EntityUid part, EntityUid woundUid, FixedPoint2? delta = null)
    {
        if (!_net.IsServer || !CanFeelPain(part) || !TryComp(woundUid, out WoundComponent? wound))
            return;

        if (!_prototypes.TryIndex(wound.Prototype, out var prototype) ||
            !prototype.TryGetBehavior(wound.Severity, out WoundPainBehavior behavior) || !behavior.OneTime)
            return;

        if (behavior.MinSeverity is { } minimum && wound.Severity < minimum)
            return;

        var amount = delta ?? wound.Severity;
        if (amount <= FixedPoint2.Zero)
            return;

        ChangePain((part, EnsureComp<PainComponent>(part)), amount * behavior.PainPerSeverity);
    }

    /// <summary>
    /// Recomputes the pain floor produced by the part's wounds. The floor is the minimum
    /// pain the part settles to while a wound is present; it does not add pain instantly
    /// (damage already does). When a wound is removed the floor drops and normal recovery
    /// decays the remaining pain down to it (accelerated decay of the wound source).
    /// </summary>
    public void RefreshWoundPain(Entity<WoundableComponent?> part)
    {
        if (!_net.IsServer || !Resolve(part, ref part.Comp, false) ||
            !TryComp(part, out PainComponent? pain))
            return;

        if (!CanFeelPain(part))
        {
            pain.WoundPain = FixedPoint2.Zero;
            SetPain((part.Owner, pain), FixedPoint2.Zero);
            Dirty(part.Owner, pain);
            return;
        }

        var floor = FixedPoint2.Zero;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (wound.Comp.State is WoundState.Healed or WoundState.Scarred ||
                !_prototypes.TryIndex(wound.Comp.Prototype, out var prototype) ||
                !prototype.TryGetBehavior(wound.Comp.Severity, out WoundPainBehavior behavior))
                continue;

            if (behavior.MinSeverity is { } minimum && wound.Comp.Severity < minimum)
                continue;

            if (behavior.OneTime)
                continue;

            floor += wound.Comp.Severity * behavior.PainPerSeverity;
        }

        if (pain.WoundPain == floor)
            return;

        pain.WoundPain = floor;
        Dirty(part, pain);
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
        while (bodyQuery.MoveNext(out var bodyUid, out var bodyPain, out var mobState, out var shockTarget))
        {
            UpdatePainShock((bodyUid, bodyPain), mobState, shockTarget);
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

        var minimum = FixedPoint2.Max(entity.Comp.WoundPain, FixedPoint2.Zero);

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

    private void UpdatePainShock(Entity<PainComponent> entity, MobStateComponent mobState,
        PainShockTargetComponent shockTarget)
    {
        if (mobState.CurrentState == MobState.Dead)
        {
            if (_statusEffects.HasStatusEffect(entity, PainShockEffect))
                _statusEffects.TryRemoveStatusEffect(entity, PainShockEffect);
            if (shockTarget.Armed)
            {
                shockTarget.Armed = false;
                Dirty(entity.Owner, shockTarget);
            }
            return;
        }

        if (mobState.CurrentState != MobState.Alive)
            return;

        var pain = GetPain((entity.Owner, entity.Comp));
        if (!shockTarget.Armed)
        {
            if (pain < PainShockRearmThreshold)
            {
                shockTarget.Armed = true;
                Dirty(entity.Owner, shockTarget);
            }
            return;
        }

        if (pain < PainShockThreshold)
            return;

        shockTarget.Armed = false;
        Dirty(entity.Owner, shockTarget);
        _statusEffects.TrySetStatusEffectDuration(entity, PainShockEffect, PainShockStunTime);
    }

    private void OnRejuvenate(Entity<PainComponent> entity, ref RejuvenateEvent args)
    {
        if (_net.IsServer)
        {
            ClearPainShock(entity);
            if (TryComp(entity, out PainShockTargetComponent? shockTarget))
            {
                shockTarget.Armed = true;
                Dirty(entity.Owner, shockTarget);
            }
        }
    }

    private void OnPainShutdown(Entity<PainComponent> entity, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            ClearPainShock(entity);
    }

    private void ClearPainShock(EntityUid entity)
    {
        _statusEffects.TryRemoveStatusEffect(entity, PainShockEffect);
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
        if (!CanFeelPain(part) || !Resolve(part, ref bodyPart, ref pain, false))
            return false;

        var change = CalculatePain(delta, pain.DamageMultipliers);
        return change != FixedPoint2.Zero && ChangePain((part, pain), change);
    }

    public bool CanFeelPain(EntityUid part)
    {
        return !TryComp(part, out WoundableComponent? woundable) ||
               !_prototypes.TryIndex(woundable.Profile, out var profile) ||
               profile.CanFeelPain;
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

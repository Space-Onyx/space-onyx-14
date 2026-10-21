// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Body.Systems;
using Content.Shared._Onyx.Consciousness;
using Content.Shared._Onyx.Traits;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private NerveSystem _nerves = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public const string WoundPainIdentifier = "WoundPain";
    public const string PainAdrenalineIdentifier = "PainAdrenaline";
    public const string SurgeryPainIdentifier = "SurgeryPain";
    public const string AdminPainIdentifier = "AdminPain";
    public const string NerveDamageFeelsIdentifier = "NerveDamage";

    private const float NerveDamagePainMultiplier = 1.4f;
    private const float NerveDamageSecondsPerPoint = 2.4f;

    private static readonly FixedPoint2 DefaultPainCap = 200;
    private float _updateAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NerveComponent, OrganGotInsertedEvent>(OnPartInserted);
        SubscribeLocalEvent<NerveComponent, OrganGotRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<NerveComponent, NerveDamageChangedEvent>(OnNerveDamageChanged);
        SubscribeLocalEvent<NervousSystemComponent, ComponentShutdown>(OnHubShutdown);
        SubscribeLocalEvent<ConsciousnessComponent, NervousSystemRebuiltEvent>(OnNervousSystemRebuilt);
        SubscribeLocalEvent<ConsciousnessComponent, NervousSystemRemovedEvent>(OnNervousSystemRemoved);
        SubscribeLocalEvent<ConsciousnessComponent, PainNumbnessChangedEvent>(OnPainNumbnessChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        _updateAccumulator += frameTime;
        if (_updateAccumulator < 1f)
            return;

        var elapsed = _updateAccumulator;
        _updateAccumulator = 0f;

        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var body, out _))
        {
            if (TerminatingOrDeleted(body) || !_consciousness.TryGetNervousSystem(body, out var hub))
                continue;

            SweepExpired(body, hub, elapsed);
            UpdateFeltPain(body, hub.Owner, hub.Comp);
            UpdateHubThreshold(body, hub);
            EnsureTraitMultipliers(body, hub);

            if (TryComp(body, out MobStateComponent? mobState))
                UpdateMobTransitions(body, hub, mobState);
        }
    }

    #region Hub and body resolution

    private bool TryGetHub(
        EntityUid part,
        out Entity<NervousSystemComponent> hub,
        out EntityUid body)
    {
        hub = default;
        body = EntityUid.Invalid;
        if (!TryComp(part, out BodyPartComponent? partComp) || partComp.Body is not { } bodyUid)
            return false;

        body = bodyUid;
        return _consciousness.TryGetNervousSystem(bodyUid, out hub);
    }

    private bool TryGetBody(Entity<NervousSystemComponent> hub, out EntityUid body)
    {
        body = EntityUid.Invalid;
        if (!TryComp(hub.Owner, out OrganComponent? organ) || organ.Body is not { } bodyUid)
            return false;

        body = bodyUid;
        return true;
    }

    #endregion

    #region Getters

    /// <summary>
    /// Felt pain of a body. Zero without a hub or while pain-numb.
    /// </summary>
    public FixedPoint2 GetPain(EntityUid body)
    {
        if (!_consciousness.TryGetNervousSystem(body, out var hub) || IsPainNumb(body))
            return FixedPoint2.Zero;

        return hub.Comp.Pain;
    }

    public FixedPoint2 GetPainCap(EntityUid body)
    {
        if (!_consciousness.TryGetNervousSystem(body, out var hub) || hub.Comp.PainCap <= FixedPoint2.Zero)
            return DefaultPainCap;

        return hub.Comp.PainCap;
    }

    /// <summary>
    /// Felt pain contributed by a single nerve (body part).
    /// </summary>
    public FixedPoint2 GetPartPain(EntityUid part)
    {
        if (!TryGetHub(part, out var hub, out var body) || IsPainNumb(body))
            return FixedPoint2.Zero;

        var pain = FixedPoint2.Zero;
        foreach (var ((nerveUid, _), modifier) in hub.Comp.Modifiers)
        {
            if (nerveUid != part)
                continue;

            pain += ApplyModifiersToPain(nerveUid, modifier.Change, hub.Comp, modifier.PainDamageType);
        }

        var cap = hub.Comp.PainCap > FixedPoint2.Zero ? hub.Comp.PainCap : DefaultPainCap;
        return FixedPoint2.Clamp(pain, FixedPoint2.Zero, cap);
    }

    private bool IsPainNumb(EntityUid body)
    {
        foreach (var effect in _statusEffects.EnumerateStatusEffects<PainNumbnessStatusEffectComponent>(body))
        {
            if (effect.Comp1.Applied)
                return true;
        }

        return false;
    }

    #endregion

    #region Pain modifiers

    public bool TryAddPainModifier(
        EntityUid hubUid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NervousSystemComponent? hub = null,
        TimeSpan? time = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        EnsureNerveAttached(hubUid, nerveUid, hub);

        var modifier = new PainModifier(change, identifier, painType, _timing.CurTime + time);
        if (!hub.Modifiers.TryAdd((nerveUid, identifier), modifier))
            return false;

        var ev = new PainModifierAddedEvent(hubUid, nerveUid, change);
        RaiseLocalEvent(hubUid, ref ev);

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    public bool TryChangePainModifier(
        EntityUid hubUid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes? painType = null,
        NervousSystemComponent? hub = null,
        TimeSpan? time = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Modifiers.TryGetValue((nerveUid, identifier), out var modifier))
            return false;

        EnsureNerveAttached(hubUid, nerveUid, hub);

        hub.Modifiers[(nerveUid, identifier)] = modifier with
        {
            Change = change,
            Time = _timing.CurTime + time ?? modifier.Time,
            PainDamageType = painType ?? modifier.PainDamageType,
        };

        var ev = new PainModifierChangedEvent(hubUid, nerveUid, change);
        RaiseLocalEvent(hubUid, ref ev);

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    public bool TryGetPainModifier(
        EntityUid hubUid,
        EntityUid nerveUid,
        string identifier,
        [NotNullWhen(true)] out PainModifier? modifier,
        NervousSystemComponent? hub = null)
    {
        modifier = null;
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Modifiers.TryGetValue((nerveUid, identifier), out var data))
            return false;

        modifier = data;
        return true;
    }

    public bool TryRemovePainModifier(
        EntityUid hubUid,
        EntityUid nerveUid,
        string identifier,
        NervousSystemComponent? hub = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Modifiers.Remove((nerveUid, identifier)))
            return false;

        var ev = new PainModifierRemovedEvent(hubUid, nerveUid, hub.Pain);
        RaiseLocalEvent(hubUid, ref ev);

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    public bool TryAddPainMultiplier(
        EntityUid hubUid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NervousSystemComponent? hub = null,
        TimeSpan? time = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Multipliers.TryAdd(identifier, new PainMultiplier(change, identifier, painType, _timing.CurTime + time)))
            return false;

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    public bool TryChangePainMultiplier(
        EntityUid hubUid,
        string identifier,
        FixedPoint2 change,
        TimeSpan? time = null,
        PainDamageTypes? painType = null,
        NervousSystemComponent? hub = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        hub.Multipliers[identifier] = multiplier with
        {
            Change = change,
            Time = _timing.CurTime + time ?? multiplier.Time,
            PainDamageType = painType ?? multiplier.PainDamageType,
        };

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    public bool TryRemovePainMultiplier(
        EntityUid hubUid,
        string identifier,
        NervousSystemComponent? hub = null)
    {
        if (!Resolve(hubUid, ref hub, false))
            return false;

        if (!hub.Multipliers.Remove(identifier))
            return false;

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    #endregion

    #region Pain feels

    public bool TryAddPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null,
        TimeSpan? time = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryAdd((effectOwner, identifier),
                new PainFeelingModifier(change, _timing.CurTime + time)))
            return false;

        UpdatePainFeels(nerveUid, nerve);
        Dirty(nerveUid, nerve);
        return true;
    }

    public bool TryGetPainFeelsModifier(
        EntityUid nerveUid,
        EntityUid effectOwner,
        string identifier,
        [NotNullWhen(true)] out PainFeelingModifier? modifier,
        NerveComponent? nerve = null)
    {
        modifier = null;
        if (!Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var data))
            return false;

        modifier = data;
        return true;
    }

    public bool TryChangePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifier with { Change = change };

        UpdatePainFeels(nerveUid, nerve);
        Dirty(nerveUid, nerve);
        return true;
    }

    public bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        TimeSpan? time = null,
        NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifier with
        {
            Change = change,
            Time = _timing.CurTime + time ?? modifier.Time,
        };

        UpdatePainFeels(nerveUid, nerve);
        Dirty(nerveUid, nerve);
        return true;
    }

    public bool TryRemovePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.Remove((effectOwner, identifier)))
            return false;

        UpdatePainFeels(nerveUid, nerve);
        Dirty(nerveUid, nerve);
        return true;
    }

    private void UpdatePainFeels(EntityUid nerveUid, NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return;

        var ev = new PainFeelsChangedEvent(nerve.ParentedNerveSystem, nerveUid, nerve.PainFeels);
        RaiseLocalEvent(nerveUid, ref ev);

        if (TryComp(nerve.ParentedNerveSystem, out NervousSystemComponent? hub))
            UpdateNerveSystemPain(nerve.ParentedNerveSystem, hub);
    }

    #endregion

    #region Surgery and admin helpers

    /// <summary>
    /// Suppresses hub pain for a fixed duration. Repeated doses accumulate, while the
    /// recovery multiplier speeds up fading pain for as long as the suppressant lasts.
    /// </summary>
    public bool SuppressPain(
        EntityUid hubUid,
        string identifier,
        FixedPoint2 amount,
        TimeSpan duration,
        float recoveryMultiplier = 1f,
        NervousSystemComponent? hub = null)
    {
        if (!_net.IsServer || string.IsNullOrWhiteSpace(identifier) || amount <= FixedPoint2.Zero ||
            duration <= TimeSpan.Zero || !float.IsFinite(recoveryMultiplier) || recoveryMultiplier < 1f ||
            !Resolve(hubUid, ref hub, false))
            return false;

        if (hub.Modifiers.TryGetValue((hubUid, identifier), out var existing))
        {
            var change = existing.Change - amount;
            hub.Modifiers[(hubUid, identifier)] = existing with
            {
                Change = change,
                Time = _timing.CurTime + duration,
                RecoveryMultiplier = Math.Max(existing.RecoveryMultiplier, recoveryMultiplier),
                DecayPerSecond = FixedPoint2.Zero,
            };

            var changed = new PainModifierChangedEvent(hubUid, hubUid, change);
            RaiseLocalEvent(hubUid, ref changed);
        }
        else
        {
            hub.Modifiers[(hubUid, identifier)] = new PainModifier(
                -amount,
                identifier,
                PainDamageTypes.WoundPain,
                _timing.CurTime + duration,
                recoveryMultiplier);

            var added = new PainModifierAddedEvent(hubUid, hubUid, -amount);
            RaiseLocalEvent(hubUid, ref added);
        }

        UpdateNerveSystemPain(hubUid, hub);
        Dirty(hubUid, hub);
        return true;
    }

    /// <summary>
    /// Strongest active recovery boost on the hub. Defaults to no boost.
    /// </summary>
    public float GetRecoveryFactor(NervousSystemComponent hub)
    {
        var factor = 1f;
        foreach (var modifier in hub.Modifiers.Values)
        {
            if (modifier.Time is { } expires && expires < _timing.CurTime)
                continue;

            factor = Math.Max(factor, modifier.RecoveryMultiplier);
        }

        return factor;
    }

    public void InflictSurgeryPain(EntityUid body, EntityUid part, FixedPoint2 amount, TimeSpan duration)
    {
        if (!_net.IsServer || amount <= FixedPoint2.Zero || !CanFeelPain(part))
            return;

        if (!_consciousness.TryGetNervousSystem(body, out var hub))
            return;

        if (!TryChangePainModifier(hub.Owner, part, SurgeryPainIdentifier, amount,
                PainDamageTypes.WoundPain, hub.Comp, duration))
        {
            TryAddPainModifier(hub.Owner, part, SurgeryPainIdentifier, amount,
                PainDamageTypes.WoundPain, hub.Comp, duration);
        }
    }

    public void RemoveAllPainEffects(EntityUid body)
    {
        if (!_net.IsServer || !_consciousness.TryGetNervousSystem(body, out var hub))
            return;

        var oldFelt = hub.Comp.LastFeltPain;

        hub.Comp.Modifiers.Clear();
        hub.Comp.Multipliers.Clear();
        hub.Comp.Pain = FixedPoint2.Zero;
        hub.Comp.LastFeltPain = FixedPoint2.Zero;
        hub.Comp.LastPainThreshold = FixedPoint2.Zero;
        hub.Comp.LastThresholdType = PainThresholdTypes.None;

        foreach (var nerveUid in hub.Comp.Nerves.ToArray())
        {
            if (!TryComp<NerveComponent>(nerveUid, out var nerve))
                continue;

            if (nerve.PainFeelingModifiers.Count > 0)
            {
                nerve.PainFeelingModifiers.Clear();
                Dirty(nerveUid, nerve);
            }

            if (nerve.MaxDamage > FixedPoint2.Zero)
                _nerves.Repair((nerveUid, nerve), nerve.MaxDamage);
        }

        Dirty(hub.Owner, hub.Comp);
        UpdatePainConsciousness(body, hub.Comp);

        if (oldFelt != FixedPoint2.Zero)
        {
            var changed = new PainChangedEvent(body, oldFelt, FixedPoint2.Zero);
            RaiseLocalEvent(body, ref changed);
        }
    }

    #endregion

    #region Pain gating

    public bool CanFeelPain(EntityUid part)
    {
        if (TryComp(part, out WoundableComponent? woundable) &&
            _prototypes.TryIndex(woundable.Profile, out var profile) &&
            !profile.CanFeelPain)
            return false;

        return HasWorkingBrain(part);
    }

    /// <summary>
    /// The brain is the center of the nervous system: a body whose anatomy
    /// requires a brain feels nothing without a functional one.
    /// </summary>
    private bool HasWorkingBrain(EntityUid entity)
    {
        var body = entity;
        if (TryComp(entity, out BodyPartComponent? partComp))
        {
            if (partComp.Body is not { } partBody)
                return true;

            body = partBody;
        }

        if (!TryComp(body, out BodyAnatomyComponent? anatomy) || !anatomy.AnatomyInitialized)
            return true;

        if (!anatomy.RequiredOrgans.ContainsKey(new ProtoId<OrganCategoryPrototype>("Brain")))
            return true;

        return _body.TryGetOrgan(body, "Brain", out var brain) &&
               TryComp(brain, out OrganComponent? organ) &&
               OrganHealthSystem.IsFunctional(organ);
    }

    #endregion

    #region Aggregation

    private void UpdateNerveSystemPain(EntityUid hubUid, NervousSystemComponent hub)
    {
        var totalPain = FixedPoint2.Zero;
        var woundPain = FixedPoint2.Zero;

        foreach (var ((nerveUid, _), modifier) in hub.Modifiers)
        {
            if (TerminatingOrDeleted(nerveUid))
                continue;

            var applied = ApplyModifiersToPain(nerveUid, modifier.Change, hub, modifier.PainDamageType);
            if (modifier.PainDamageType == PainDamageTypes.WoundPain && applied > FixedPoint2.Zero)
                woundPain += applied;

            totalPain += applied;
        }

        var softCap = FixedPoint2.Max(FixedPoint2.Zero, hub.SoftPainCap);
        var hardCap = hub.PainCap > FixedPoint2.Zero ? hub.PainCap : DefaultPainCap;
        var newPain = FixedPoint2.Clamp(
            FixedPoint2.Min(woundPain, softCap) + totalPain - woundPain,
            FixedPoint2.Zero,
            hardCap);

        if (!TryGetBody((hubUid, hub), out var body))
        {
            hub.UpdateTime = _timing.CurTime + hub.ThresholdUpdateTime;
            if (hub.Pain != newPain)
                hub.ReactionUpdateTime = _timing.CurTime + hub.PainReactionTime;
            hub.Pain = newPain;
            Dirty(hubUid, hub);
            return;
        }

        hub.UpdateTime = _timing.CurTime + hub.ThresholdUpdateTime;
        if (hub.Pain != newPain)
            hub.ReactionUpdateTime = _timing.CurTime + hub.PainReactionTime;
        hub.Pain = newPain;
        Dirty(hubUid, hub);

        UpdateFeltPain(body, hubUid, hub);
    }

    private FixedPoint2 ApplyModifiersToPain(
        EntityUid nerveUid,
        FixedPoint2 pain,
        NervousSystemComponent hub,
        PainDamageTypes painType,
        NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false))
            return pain;

        var modifiedPain = pain * nerve.PainMultiplier;

        var toMultiply = FixedPoint2.Zero;
        var matching = 0;
        foreach (var multiplier in hub.Multipliers.Values)
        {
            if (multiplier.PainDamageType != painType)
                continue;

            toMultiply += multiplier.Change;
            matching++;
        }

        if (matching == 0)
            return modifiedPain;

        return modifiedPain * toMultiply / (float) matching;
    }

    private void UpdatePainConsciousness(
        EntityUid body,
        NervousSystemComponent hub,
        FixedPoint2? pain = null)
    {
        var feltPain = pain ?? (IsPainNumb(body) ? FixedPoint2.Zero : hub.Pain);
        _consciousness.SetConsciousnessModifier(
            body,
            body,
            -feltPain,
            "Pain",
            ConsciousnessModType.Pain);
    }

    private void UpdateFeltPain(EntityUid body, EntityUid hubUid, NervousSystemComponent hub)
    {
        var feltPain = IsPainNumb(body) ? FixedPoint2.Zero : hub.Pain;
        UpdatePainConsciousness(body, hub, feltPain);
        if (hub.LastFeltPain == feltPain)
            return;

        var changed = new PainChangedEvent(body, hub.LastFeltPain, feltPain);
        hub.LastFeltPain = feltPain;
        Dirty(hubUid, hub);
        RaiseLocalEvent(body, ref changed);
    }

    #endregion

    #region Expiry and thresholds

    private void SweepExpired(EntityUid body, Entity<NervousSystemComponent> hub, float elapsed)
    {
        var changed = false;
        var recovery = GetRecoveryFactor(hub.Comp);

        foreach (var (key, modifier) in hub.Comp.Modifiers.ToArray())
        {
            if (modifier.DecayPerSecond > FixedPoint2.Zero && modifier.Change != FixedPoint2.Zero)
            {
                var decay = modifier.DecayPerSecond * recovery * elapsed;
                var change = modifier.Change < FixedPoint2.Zero
                    ? FixedPoint2.Min(FixedPoint2.Zero, modifier.Change + decay)
                    : FixedPoint2.Max(FixedPoint2.Zero, modifier.Change - decay);
                if (change != FixedPoint2.Zero)
                {
                    hub.Comp.Modifiers[key] = modifier with { Change = change };
                    changed = true;
                    continue;
                }

                hub.Comp.Modifiers.Remove(key);
                var decayed = new PainModifierRemovedEvent(hub.Owner, key.Item1, hub.Comp.Pain);
                RaiseLocalEvent(hub.Owner, ref decayed);
                changed = true;
                continue;
            }

            if (!TerminatingOrDeleted(key.Item1) &&
                (modifier.Time is null || modifier.Time >= _timing.CurTime))
            {
                continue;
            }

            hub.Comp.Modifiers.Remove(key);
            var removed = new PainModifierRemovedEvent(hub.Owner, key.Item1, hub.Comp.Pain);
            RaiseLocalEvent(hub.Owner, ref removed);
            changed = true;
        }

        foreach (var (key, multiplier) in hub.Comp.Multipliers)
        {
            if (multiplier.Time is null || multiplier.Time >= _timing.CurTime)
                continue;

            hub.Comp.Multipliers.Remove(key);
            changed = true;
        }

        foreach (var nerveUid in hub.Comp.Nerves.ToArray())
        {
            if (!TryComp<NerveComponent>(nerveUid, out var nerve))
            {
                hub.Comp.Nerves.Remove(nerveUid);
                continue;
            }

            foreach (var (key, feels) in nerve.PainFeelingModifiers.ToArray())
            {
                if (feels.Time is null || feels.Time >= _timing.CurTime)
                    continue;

                nerve.PainFeelingModifiers.Remove(key);
                UpdatePainFeels(nerveUid, nerve);
                Dirty(nerveUid, nerve);
            }
        }

        Dirty(hub.Owner, hub.Comp);

        if (changed)
            UpdateNerveSystemPain(hub.Owner, hub.Comp);
    }

    private void UpdateHubThreshold(EntityUid body, Entity<NervousSystemComponent> hub)
    {
        if (hub.Comp.LastPainThreshold == hub.Comp.Pain)
            return;

        if (_timing.CurTime > hub.Comp.UpdateTime)
            hub.Comp.LastPainThreshold = hub.Comp.Pain;

        if (_timing.CurTime > hub.Comp.ReactionUpdateTime)
            UpdatePainThreshold(body, hub);

        Dirty(hub.Owner, hub.Comp);
    }

    private void UpdatePainThreshold(EntityUid body, Entity<NervousSystemComponent> hub)
    {
        var painInput = hub.Comp.Pain - hub.Comp.LastPainThreshold;

        var nearest = PainThresholdTypes.None;
        hub.Comp.SortedPainThresholds ??= hub.Comp.PainThresholds.OrderByDescending(kv => kv.Value).ToArray();
        foreach (var (reflex, threshold) in hub.Comp.SortedPainThresholds)
        {
            if (painInput < threshold)
                continue;

            nearest = reflex;
            break;
        }

        if (nearest == PainThresholdTypes.None)
            return;

        if (hub.Comp.LastThresholdType == nearest && _timing.CurTime < hub.Comp.UpdateTime)
            return;

        var triggered = new PainThresholdTriggered(hub, nearest, painInput);
        RaiseLocalEvent(body, ref triggered);

        if (triggered.Cancelled || _mobState.IsDead(body))
            return;

        var effected = new PainThresholdEffected(hub, nearest, painInput);
        RaiseLocalEvent(body, ref effected);

        hub.Comp.LastThresholdType = nearest;
        Dirty(hub.Owner, hub.Comp);

        ApplyPainReflexesEffects(body, hub, nearest);
    }

    private void ApplyPainReflexesEffects(
        EntityUid body,
        Entity<NervousSystemComponent> hub,
        PainThresholdTypes reaction)
    {
        if (!_net.IsServer)
            return;

        var sex = GetBodySex(body);

        switch (reaction)
        {
            case PainThresholdTypes.PainFlinch:
                CleanupPainScreams(hub.Comp);
                PlayPainScream(body, hub.Comp, hub.Comp.PainScreams[sex], null,
                    Loc.GetString("screams-and-flinches-pain", ("entity", body)));
                _jitter.DoJitter(body, TimeSpan.FromSeconds(0.9f), true, 24f, 1f);
                break;

            case PainThresholdTypes.Agony:
                CleanupPainScreams(hub.Comp);
                PlayPainScream(body, hub.Comp, hub.Comp.AgonyScreams[sex],
                    AudioParams.Default.WithVolume(12f),
                    Loc.GetString("screams-in-agony", ("entity", body)));
                _audio.PlayPvs(hub.Comp.PainRattles, body, AudioParams.Default.WithVolume(-12f));
                _jitter.DoJitter(body, hub.Comp.PainShockStunTime / 1.4, true, 30f, 12f);
                break;

            case PainThresholdTypes.PainShock:
                CleanupPainScreams(hub.Comp);
                var down = TryComp(body, out StandingStateComponent? standing) &&
                    _standing.IsDown((body, standing));
                PlayPainScream(body, hub.Comp, hub.Comp.PainShockScreams[sex],
                    AudioParams.Default.WithVolume(12f),
                    Loc.GetString(down ? "screams-in-pain" : "screams-and-falls-pain", ("entity", body)));

                TryAddPainMultiplier(hub.Owner,
                    PainAdrenalineIdentifier,
                    FixedPoint2.New(0.7f),
                    PainDamageTypes.WoundPain,
                    hub.Comp,
                    hub.Comp.PainShockAdrenalineTime);

                _stun.TryUpdateParalyzeDuration(body, hub.Comp.PainShockStunTime);
                _jitter.DoJitter(body, hub.Comp.PainShockStunTime, true, 20f, 7f);
                _consciousness.ForceConscious(body, hub.Comp.PainShockStunTime);
                break;

            case PainThresholdTypes.PainShockAndAgony:
                CleanupPainScreams(hub.Comp);
                var shockDown = TryComp(body, out StandingStateComponent? shockStanding) &&
                    _standing.IsDown((body, shockStanding));
                PlayPainScream(body, hub.Comp, hub.Comp.AgonyScreams[sex],
                    AudioParams.Default.WithVolume(12f),
                    Loc.GetString(shockDown ? "screams-in-pain" : "screams-and-falls-pain", ("entity", body)));

                _stun.TryUpdateParalyzeDuration(body, hub.Comp.PainShockStunTime * 1.4);
                _jitter.DoJitter(body, hub.Comp.PainShockStunTime * 1.4, true, 20f, 7f);
                _consciousness.ForceConscious(body, hub.Comp.PainShockStunTime * 1.4);
                break;

            case PainThresholdTypes.None:
                break;
        }
    }

    private void UpdateMobTransitions(EntityUid body, Entity<NervousSystemComponent> hub, MobStateComponent mobState)
    {
        if (mobState.CurrentState != hub.Comp.LastMobState)
        {
            if (mobState.CurrentState == MobState.Critical)
            {
                PlayPainScream(body, hub.Comp, hub.Comp.CritWhimpers[GetBodySex(body)],
                    AudioParams.Default.WithVolume(-12f));
                hub.Comp.NextCritScream = _timing.CurTime + NextCritScreamDelay(hub.Comp);
            }
            else if (mobState.CurrentState == MobState.Dead)
            {
                CleanupPainScreams(hub.Comp);
            }

            hub.Comp.LastMobState = mobState.CurrentState;
            Dirty(hub.Owner, hub.Comp);
        }

        if (mobState.CurrentState == MobState.Critical && _timing.CurTime >= hub.Comp.NextCritScream)
        {
            PlayPainScream(body, hub.Comp, hub.Comp.CritWhimpers[GetBodySex(body)],
                AudioParams.Default.WithVolume(-12f));
            hub.Comp.NextCritScream = _timing.CurTime + NextCritScreamDelay(hub.Comp);
            Dirty(hub.Owner, hub.Comp);
        }
    }

    #endregion

    #region Nerve membership and damage

    /// <summary>
    /// Attaches a nerve to the hub if it is not parented yet. Covers nerves that
    /// gained their component after the insertion event was raised.
    /// </summary>
    private void EnsureNerveAttached(EntityUid hubUid, EntityUid nerveUid, NervousSystemComponent hub)
    {
        if (nerveUid == hubUid || !TryComp<NerveComponent>(nerveUid, out var nerve))
            return;

        if (nerve.ParentedNerveSystem == hubUid && hub.Nerves.Contains(nerveUid))
            return;

        nerve.ParentedNerveSystem = hubUid;
        Dirty(nerveUid, nerve);
        hub.Nerves.Add(nerveUid);
    }

    /// <summary>
    /// Reapplies the high pain threshold trait multiplier if the body has the trait
    /// but the hub lost it (transplant, late trait gain). Removal is owned by the trait system.
    /// </summary>
    private void EnsureTraitMultipliers(EntityUid body, Entity<NervousSystemComponent> hub)
    {
        if (!TryComp(body, out HighPainThresholdComponent? threshold) ||
            hub.Comp.Multipliers.ContainsKey(nameof(HighPainThresholdComponent)))
            return;

        TryAddPainMultiplier(hub.Owner, nameof(HighPainThresholdComponent),
            FixedPoint2.New(threshold.PainMultiplier), PainDamageTypes.WoundPain, hub.Comp);
    }

    private void OnPartInserted(Entity<NerveComponent> nerve, ref OrganGotInsertedEvent args)
    {
        if (!_net.IsServer || TerminatingOrDeleted(nerve.Owner) || TerminatingOrDeleted(args.Target))
            return;

        if (!_consciousness.TryGetNervousSystem(args.Target, out var hub))
            return;

        hub.Comp.Nerves.Add(nerve.Owner);
        nerve.Comp.ParentedNerveSystem = hub.Owner;
        Dirty(nerve.Owner, nerve.Comp);
        Dirty(hub.Owner, hub.Comp);

        RecomputeWoundPain(nerve.Owner);
    }

    private void OnPartRemoved(Entity<NerveComponent> nerve, ref OrganGotRemovedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!_consciousness.TryGetNervousSystem(args.Target, out var hub))
            return;

        var removed = false;
        foreach (var key in hub.Comp.Modifiers.Keys.ToArray())
        {
            if (key.Item1 != nerve.Owner)
                continue;

            hub.Comp.Modifiers.Remove(key);
            removed = true;
        }

        hub.Comp.Nerves.Remove(nerve.Owner);
        if (!TerminatingOrDeleted(nerve.Owner))
        {
            nerve.Comp.ParentedNerveSystem = EntityUid.Invalid;
            Dirty(nerve.Owner, nerve.Comp);
        }

        Dirty(hub.Owner, hub.Comp);

        if (removed)
            UpdateNerveSystemPain(hub.Owner, hub.Comp);
    }

    private void OnNerveDamageChanged(Entity<NerveComponent> nerve, ref NerveDamageChangedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Damage > FixedPoint2.Zero)
        {
            var delta = args.Damage - args.OldDamage;
            TimeSpan? duration = delta > FixedPoint2.Zero
                ? TimeSpan.FromSeconds(Math.Max(1f, delta.Float() * NerveDamageSecondsPerPoint))
                : null;

            if (!TryAddPainFeelsModifier(nerve.Owner, NerveDamageFeelsIdentifier, nerve.Owner,
                    FixedPoint2.New(-0.28f), nerve.Comp, duration))
            {
                TrySetPainFeelsModifier(nerve.Owner, NerveDamageFeelsIdentifier, nerve.Owner,
                    FixedPoint2.New(-0.28f), duration, nerve.Comp);
            }

            if (TryComp(nerve.Comp.ParentedNerveSystem, out NervousSystemComponent? hub))
            {
                var hubUid = nerve.Comp.ParentedNerveSystem;
                if (!TryAddPainMultiplier(hubUid, NerveDamageFeelsIdentifier,
                        FixedPoint2.New(NerveDamagePainMultiplier), PainDamageTypes.WoundPain, hub, duration))
                {
                    TryChangePainMultiplier(hubUid, NerveDamageFeelsIdentifier,
                        FixedPoint2.New(NerveDamagePainMultiplier), duration, PainDamageTypes.WoundPain, hub);
                }
            }
        }
        else
        {
            TryRemovePainFeelsModifier(nerve.Owner, NerveDamageFeelsIdentifier, nerve.Owner, nerve.Comp);

            if (TryComp(nerve.Comp.ParentedNerveSystem, out NervousSystemComponent? hub))
                TryRemovePainMultiplier(nerve.Comp.ParentedNerveSystem, NerveDamageFeelsIdentifier, hub);
        }
    }

    private void OnNervousSystemRebuilt(Entity<ConsciousnessComponent> body, ref NervousSystemRebuiltEvent args)
    {
        if (!_net.IsServer || !TryComp(args.NerveSystem, out NervousSystemComponent? hub))
            return;

        foreach (var nerveUid in hub.Nerves)
            RecomputeWoundPain(nerveUid);
    }

    private void OnNervousSystemRemoved(Entity<ConsciousnessComponent> body, ref NervousSystemRemovedEvent args)
    {
        if (!_net.IsServer || !TryComp(args.NerveSystem, out NervousSystemComponent? hub))
            return;

        var oldPain = hub.LastFeltPain;
        DetachNerves((args.NerveSystem, hub));
        hub.Modifiers.Clear();
        hub.Multipliers.Clear();
        hub.Pain = FixedPoint2.Zero;
        hub.LastFeltPain = FixedPoint2.Zero;
        hub.LastPainThreshold = FixedPoint2.Zero;
        hub.LastThresholdType = PainThresholdTypes.None;
        Dirty(args.NerveSystem, hub);
        UpdatePainConsciousness(body, hub);

        if (oldPain != FixedPoint2.Zero)
        {
            var changed = new PainChangedEvent(body, oldPain, FixedPoint2.Zero);
            RaiseLocalEvent(body, ref changed);
        }
    }

    private void OnPainNumbnessChanged(Entity<ConsciousnessComponent> body, ref PainNumbnessChangedEvent args)
    {
        if (_net.IsServer && _consciousness.TryGetNervousSystem(body, out var hub))
            UpdateFeltPain(body, hub.Owner, hub.Comp);
    }

    private void OnHubShutdown(Entity<NervousSystemComponent> hub, ref ComponentShutdown args)
    {
        CleanupPainScreams(hub.Comp);

        DetachNerves(hub);
    }

    private void DetachNerves(Entity<NervousSystemComponent> hub)
    {
        foreach (var nerveUid in hub.Comp.Nerves)
        {
            if (!TryComp(nerveUid, out NerveComponent? nerve) || nerve.ParentedNerveSystem != hub.Owner)
                continue;

            nerve.ParentedNerveSystem = EntityUid.Invalid;
            Dirty(nerveUid, nerve);
        }

        hub.Comp.Nerves.Clear();
    }

    #endregion

    #region Screams

    private void PlayPainScream(
        EntityUid body,
        NervousSystemComponent hub,
        SoundSpecifier specifier,
        AudioParams? audioParams = null,
        string? screamString = null)
    {
        if (!TryComp(body, out ConsciousnessComponent? consciousness) || !consciousness.HasPainScreams)
            return;

        if (!_random.Prob(hub.ScreamChance) || _mobState.IsDead(body))
            return;

        CleanupPainScreams(hub);

        var sound = _audio.PlayPvs(specifier, body, audioParams);
        if (sound == null)
            return;

        if (screamString != null)
            _popup.PopupEntity(screamString, body, (EntityUid?) null, PopupType.MediumCaution);

        hub.PlayedPainScreams.Add(sound.Value.Entity);
    }

    private void CleanupPainScreams(NervousSystemComponent hub)
    {
        foreach (var id in hub.PlayedPainScreams.ToArray())
        {
            if (!TerminatingOrDeleted(id))
                _audio.Stop(id);

            hub.PlayedPainScreams.Remove(id);
        }
    }

    private TimeSpan NextCritScreamDelay(NervousSystemComponent hub)
    {
        var span = (float) (hub.CritScreamsIntervalMax - hub.CritScreamsIntervalMin).TotalSeconds;
        return hub.CritScreamsIntervalMin + TimeSpan.FromSeconds(_random.NextFloat() * Math.Max(0f, span));
    }

    private Sex GetBodySex(EntityUid body)
    {
        if (TryComp(body, out HumanoidProfileComponent? humanoid))
            return humanoid.Sex;

        return Sex.Unsexed;
    }

    #endregion
}

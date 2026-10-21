// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Consciousness;

public sealed partial class ConsciousnessSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>
    /// Identifier of the organ carrying the nervous system (brain).
    /// The nervous hub exists only while this organ is in the body.
    /// </summary>
    public const string NerveSystemIdentifier = "nerveSystem";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganGotInsertedEvent>(OnRequiredInserted);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganGotRemovedEvent>(OnRequiredRemoved);
        SubscribeLocalEvent<ConsciousnessComponent, ComponentStartup>(OnConsciousnessStartup);
        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var uid, out var consciousness))
        {
            if (consciousness.ForceDead || _timing.CurTime < consciousness.NextConsciousnessUpdate)
                continue;

            consciousness.NextConsciousnessUpdate = _timing.CurTime + consciousness.ConsciousnessUpdateTime;

            foreach (var modifier in consciousness.Modifiers.Where(modifier => modifier.Value.Time < _timing.CurTime).ToArray())
                RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, consciousness);

            foreach (var multiplier in consciousness.Multipliers.Where(multiplier => multiplier.Value.Time < _timing.CurTime).ToArray())
                RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, consciousness);

            if (consciousness is { PassedOut: true } && consciousness.PassedOutTime < _timing.CurTime)
            {
                consciousness.PassedOut = false;
                Dirty(uid, consciousness);
                CheckConscious(uid, consciousness);
            }

            if (consciousness is { ForceConscious: true } && consciousness.ForceConsciousnessTime < _timing.CurTime)
            {
                consciousness.ForceConscious = false;
                Dirty(uid, consciousness);
                CheckConscious(uid, consciousness);
            }
        }
    }

    /// <summary>
    /// Gets the nervous hub (brain organ) of a body, if it has one.
    /// </summary>
    public bool TryGetNervousSystem(
        EntityUid body,
        out Entity<NervousSystemComponent> nervous,
        ConsciousnessComponent? consciousness = null)
    {
        nervous = default;
        if (!Resolve(body, ref consciousness, false))
            return false;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(NerveSystemIdentifier, out var entry) ||
            entry.Item1 is not { } hub ||
            entry.Item3 ||
            TerminatingOrDeleted(hub) ||
            !TryComp(hub, out NervousSystemComponent? hubComp))
            return false;

        nervous = (hub, hubComp);
        return true;
    }

    public bool CheckConscious(EntityUid target, ConsciousnessComponent? consciousness = null, MobStateComponent? mobState = null)
    {
        if (!Resolve(target, ref consciousness, ref mobState, false))
            return false;

        var shouldBeConscious = consciousness is { ForceDead: false, ForceUnconscious: false } &&
            (consciousness.Consciousness > consciousness.Threshold || consciousness.ForceConscious);

        if (shouldBeConscious != consciousness.IsConscious)
        {
            var ev = new ConsciousnessUpdatedEvent(shouldBeConscious);
            RaiseLocalEvent(target, ref ev);
        }

        consciousness.IsConscious = shouldBeConscious;
        Dirty(target, consciousness);
        UpdateMobState(target, consciousness, mobState);

        return shouldBeConscious;
    }

    public void ForcePassOut(EntityUid target, TimeSpan time, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return;

        consciousness.PassedOut = true;
        consciousness.PassedOutTime = _timing.CurTime + time;
        Dirty(target, consciousness);

        CheckConscious(target, consciousness);
    }

    public void ForceConscious(EntityUid target, TimeSpan time, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return;

        consciousness.ForceConscious = true;
        consciousness.ForceConsciousnessTime = _timing.CurTime + time;
        Dirty(target, consciousness);

        CheckConscious(target, consciousness);
    }

    public bool AddConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Modifiers.TryAdd((modifierOwner, identifier),
                new ConsciousnessModifier(modifier, _timing.CurTime + time, type)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        return true;
    }

    public bool SetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        consciousness.Modifiers[(modifierOwner, identifier)] =
            new ConsciousnessModifier(modifierChange, _timing.CurTime + time, type);

        UpdateConsciousnessModifiers(target, consciousness);
        return true;
    }

    public bool RemoveConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Modifiers.Remove((modifierOwner, identifier)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        return true;
    }

    public bool TryGetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        [NotNullWhen(true)] out ConsciousnessModifier? modifier,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        modifier = null;
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner, identifier), out var rawModifier))
            return false;

        modifier = rawModifier;
        return true;
    }

    public bool AddConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Multipliers.TryAdd((multiplierOwner, identifier),
                new ConsciousnessMultiplier(multiplier, _timing.CurTime + time ?? time, type)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        return true;
    }

    public bool RemoveConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Multipliers.Remove((multiplierOwner, identifier)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        return true;
    }

    private void UpdateConsciousnessModifiers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!Resolve(uid, ref consciousness))
            return;

        var total = FixedPoint2.Zero;
        foreach (var modifier in consciousness.Modifiers.Values)
            total += modifier.Change * consciousness.Multiplier;

        consciousness.RawConsciousness = consciousness.Cap + total;

        CheckConscious(uid, consciousness);
        Dirty(uid, consciousness);
    }

    private void UpdateConsciousnessMultipliers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!Resolve(uid, ref consciousness))
            return;

        var total = FixedPoint2.Zero;
        foreach (var multiplier in consciousness.Multipliers.Values)
            total += multiplier.Change;
        consciousness.Multiplier = consciousness.Multipliers.Count > 0
            ? total / consciousness.Multipliers.Count
            : FixedPoint2.New(1f);

        UpdateConsciousnessModifiers(uid, consciousness);
    }

    /// <summary>
    /// Couples consciousness to mob state, one direction only: losing
    /// consciousness while alive knocks the mob into critical. Recovery is
    /// owned by the damage threshold logic, never forced here.
    /// </summary>
    private void UpdateMobState(EntityUid target, ConsciousnessComponent? consciousness = null, MobStateComponent? mobState = null)
    {
        if (_net.IsClient || TerminatingOrDeleted(target) || !Resolve(target, ref consciousness, ref mobState))
            return;

        if (consciousness.ForceDead)
        {
            if (!consciousness.IsConscious && mobState.CurrentState != MobState.Dead)
                _mobState.ChangeMobState(target, MobState.Dead, mobState);
            return;
        }

        if (!consciousness.IsConscious && mobState.CurrentState == MobState.Alive)
            _mobState.ChangeMobState(target, MobState.Critical, mobState);
    }

    private void CheckRequiredParts(EntityUid body, ConsciousnessComponent consciousness)
    {
        var alive = true;
        var conscious = true;

        foreach (var (_, (_, forcesDeath, isLost)) in consciousness.RequiredConsciousnessParts)
        {
            if (!isLost)
                continue;

            if (forcesDeath)
            {
                alive = false;
                break;
            }

            conscious = false;
        }

        if (alive)
        {
            consciousness.ForceDead = false;
            consciousness.ForceUnconscious = !conscious;
        }
        else
        {
            consciousness.ForceDead = true;
        }

        Dirty(body, consciousness);
        CheckConscious(body, consciousness);
    }

    private void OnMobStateChanged(EntityUid uid, ConsciousnessComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        AddConsciousnessModifier(uid, uid, -component.Cap, "DeathThreshold", ConsciousnessModType.Pain, consciousness: component);

        foreach (var multiplier in component.Multipliers
                     .Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain).ToArray())
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);

        foreach (var modifier in component.Modifiers
                     .Where(modifier => modifier.Value.Type != ConsciousnessModType.Pain).ToArray())
            RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, component);
    }

    private void OnRejuvenate(EntityUid uid, ConsciousnessComponent component, RejuvenateEvent args)
    {
        foreach (var multiplier in component.Multipliers
                     .Where(multiplier => multiplier.Value.Type == ConsciousnessModType.Pain).ToArray())
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);

        foreach (var modifier in component.Modifiers
                     .Where(modifier => modifier.Value.Type == ConsciousnessModType.Pain).ToArray())
            RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, component);

        CheckRequiredParts(uid, component);
        ForceConscious(uid, TimeSpan.FromSeconds(1f), component);
    }

    private void OnMapInit(EntityUid uid, ConsciousnessComponent consciousness, MapInitEvent args)
    {
        SynchronizeRequiredParts((uid, consciousness));

        if (consciousness.RawConsciousness < FixedPoint2.Zero)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(uid, consciousness);
        }

        CheckConscious(uid, consciousness);
    }

    private void OnRequiredInserted(Entity<ConsciousnessRequiredComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (!TryComp(args.Target, out ConsciousnessComponent? consciousness))
            return;

        if (consciousness.RequiredConsciousnessParts.TryGetValue(ent.Comp.Identifier, out var existing) &&
            existing.Item1 is { } current && current != ent.Owner)
        {
            Log.Warning("Duplicate consciousness requirement {Identifier} on {Body}: {First} and {Second}.",
                ent.Comp.Identifier,
                ToPrettyString(args.Target),
                ToPrettyString(current),
                ToPrettyString(ent.Owner));
        }

        consciousness.RequiredConsciousnessParts[ent.Comp.Identifier] = (ent.Owner, ent.Comp.CausesDeath, false);
        Dirty(args.Target, consciousness);
        CheckRequiredParts(args.Target, consciousness);

        if (ent.Comp.Identifier == NerveSystemIdentifier)
            BuildNerveHub(args.Target, ent.Owner);
    }

    private void OnConsciousnessStartup(Entity<ConsciousnessComponent> ent, ref ComponentStartup args)
    {
        SynchronizeRequiredParts(ent);
    }

    private void SynchronizeRequiredParts(Entity<ConsciousnessComponent> ent)
    {
        foreach (var (organId, _) in _body.GetBodyOrgans(ent))
        {
            if (!TryComp(organId, out ConsciousnessRequiredComponent? required))
                continue;

            ent.Comp.RequiredConsciousnessParts[required.Identifier] = (organId, required.CausesDeath, false);
        }

        Dirty(ent);

        if (ent.Comp.RequiredConsciousnessParts.TryGetValue(NerveSystemIdentifier, out var entry) &&
            entry.Item1 is { } hub)
            BuildNerveHub(ent.Owner, hub);
    }

    /// <summary>
    /// (Re)builds the nervous hub nerve list from the body's current parts.
    /// Runs both when the nerve organ is inserted and when consciousness appears later,
    /// since body assembly can precede the consciousness component.
    /// </summary>
    private void BuildNerveHub(EntityUid body, EntityUid hub)
    {
        if (TerminatingOrDeleted(hub))
            return;

        var hubComp = EnsureComp<NervousSystemComponent>(hub);
        hubComp.Nerves.Clear();
        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp(partId, out NerveComponent? nerve))
                continue;

            hubComp.Nerves.Add(partId);
            nerve.ParentedNerveSystem = hub;
            Dirty(partId, nerve);
        }
        Dirty(hub, hubComp);

        var rebuilt = new NervousSystemRebuiltEvent(hub);
        RaiseLocalEvent(body, ref rebuilt);
    }

    private void OnRequiredRemoved(Entity<ConsciousnessRequiredComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!TryComp(args.Target, out ConsciousnessComponent? consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(ent.Comp.Identifier, out var existing))
        {
            Log.Warning("Consciousness requirement {Identifier} of {Organ} not tracked on {Body}.",
                ent.Comp.Identifier,
                ToPrettyString(ent.Owner),
                ToPrettyString(args.Target));
            return;
        }

        consciousness.RequiredConsciousnessParts[ent.Comp.Identifier] = (ent.Owner, existing.Item2, true);
        Dirty(args.Target, consciousness);
        CheckRequiredParts(args.Target, consciousness);

        if (ent.Comp.Identifier == NerveSystemIdentifier)
        {
            var removed = new NervousSystemRemovedEvent(ent.Owner);
            RaiseLocalEvent(args.Target, ref removed);
        }
    }
}

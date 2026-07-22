using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly Dictionary<ProtoId<DamageTypePrototype>, WoundPrototype> _woundsByDamageType = new();

    public override void Initialize()
    {
        base.Initialize();
        RebuildDamageTypeCache();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<WoundableComponent, RejuvenateEvent>(OnPartRejuvenate);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<WoundPrototype>())
            RebuildDamageTypeCache();
    }

    private void RebuildDamageTypeCache()
    {
        _woundsByDamageType.Clear();
        foreach (var prototype in _prototypes.EnumeratePrototypes<WoundPrototype>())
        {
            foreach (var type in prototype.DamageTypes)
                _woundsByDamageType.TryAdd(type, prototype);
        }
    }

    private void OnWoundableInit(Entity<WoundableComponent> part, ref ComponentInit args)
    {
        part.Comp.WoundsContainer = _containers.EnsureContainer<Container>(part, WoundableComponent.ContainerId);
    }

    internal void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount == FixedPoint2.Zero || !TryResolvePrototype(type, out var prototype))
                continue;

            if (amount > FixedPoint2.Zero)
                CreateOrMergeWound(part.Owner, prototype, amount);
            else
                HealWounds(part, prototype, -amount);
        }
    }

    private void OnRejuvenate(Entity<BodyComponent> body, ref RejuvenateEvent args)
    {
        if (!_net.IsServer || !HasComp<WoundHostComponent>(body))
            return;

        foreach (var (part, _) in _body.GetBodyChildren(body))
            ClearWounds(part);
    }

    private void OnPartRejuvenate(Entity<WoundableComponent> part, ref RejuvenateEvent args)
    {
        if (_net.IsServer)
            ClearWounds(part.Owner);
    }

    public IEnumerable<Entity<WoundComponent>> GetWounds(Entity<WoundableComponent?> part)
    {
        if (!Resolve(part, ref part.Comp, false))
            yield break;

        foreach (var wound in part.Comp.WoundsContainer.ContainedEntities)
            if (TryComp(wound, out WoundComponent? component))
                yield return (wound, component);
    }

    public EntityUid? CreateOrMergeWound(Entity<WoundableComponent?> part, ProtoId<WoundPrototype> prototypeId, FixedPoint2 severity)
    {
        if (!_net.IsServer || severity <= FixedPoint2.Zero ||
            !Resolve(part, ref part.Comp, false) || !_prototypes.TryIndex(prototypeId, out var prototype))
            return null;

        if (prototype.MergeMode == WoundMergeMode.MergeByPrototype)
        {
            foreach (var wound in GetWounds(part))
            {
                if (wound.Comp.Prototype == prototypeId && wound.Comp.State is not WoundState.Healed and not WoundState.Scarred)
                {
                    if (wound.Comp.State != WoundState.Open)
                        SetWoundState(wound.Owner, WoundState.Open);
                    if (!HasComp<WoundBleedingComponent>(wound) && prototype.BleedingRate > 0f &&
                        _random.Prob(Math.Clamp(prototype.BleedingChance, 0f, 1f)))
                    {
                        var bleeding = AddComp<WoundBleedingComponent>(wound.Owner);
                        bleeding.BleedingSeverity = FixedPoint2.Zero;
                    }
                    ChangeSeverity(wound.Owner, severity);
                    return wound;
                }
            }
        }

        var woundId = Spawn(null, MapCoordinates.Nullspace);
        var component = AddComp<WoundComponent>(woundId);
        component.HoldingPart = part;
        component.Prototype = prototypeId;
        component.Severity = FixedPoint2.Min(severity, prototype.MaximumSeverity);
        component.PeakSeverity = component.Severity;
        Dirty(woundId, component);

        if (prototype.BleedingRate > 0f && _random.Prob(Math.Clamp(prototype.BleedingChance, 0f, 1f)))
        {
            var bleeding = AddComp<WoundBleedingComponent>(woundId);
            bleeding.BleedingSeverity = component.Severity;
        }

        if (!_containers.Insert(woundId, part.Comp.WoundsContainer))
        {
            QueueDel(woundId);
            return null;
        }

        var created = new WoundCreatedEvent(part, woundId, prototypeId);
        RaiseLocalEvent(part, ref created);
        RaiseLocalEvent(woundId, ref created);
        return woundId;
    }

    public bool ChangeSeverity(Entity<WoundComponent?> wound, FixedPoint2 delta)
    {
        if (!_net.IsServer || !Resolve(wound, ref wound.Comp, false) ||
            HasComp<WoundScarComponent>(wound) || !_prototypes.TryIndex(wound.Comp.Prototype, out var prototype))
            return false;

        var old = wound.Comp.Severity;
        var severity = FixedPoint2.Min(prototype.MaximumSeverity, FixedPoint2.Max(FixedPoint2.Zero, old + delta));
        if (severity == old)
            return false;

        if (severity == FixedPoint2.Zero)
        {
            wound.Comp.Severity = FixedPoint2.Zero;
            Dirty(wound);
            var healed = new WoundChangedEvent(wound.Comp.HoldingPart, wound, old, FixedPoint2.Zero);
            RaiseLocalEvent(wound.Comp.HoldingPart, ref healed);
            RaiseLocalEvent(wound, ref healed);
            CloseWound(wound);
            return RemoveWound(wound);
        }

        wound.Comp.Severity = severity;
        wound.Comp.PeakSeverity = FixedPoint2.Max(wound.Comp.PeakSeverity, severity);
        Dirty(wound);
        var changed = new WoundChangedEvent(wound.Comp.HoldingPart, wound, old, severity);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref changed);
        RaiseLocalEvent(wound, ref changed);
        return true;
    }

    public bool TreatWound(Entity<WoundComponent?> wound, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero || !Resolve(wound, ref wound.Comp, false))
            return false;

        var attempt = new WoundTreatmentAttemptEvent(wound.Comp.HoldingPart, wound, amount);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref attempt);
        RaiseLocalEvent(wound, ref attempt);
        return !attempt.Cancelled && ChangeSeverity(wound, -amount);
    }

    public bool SetWoundState(Entity<WoundComponent?> wound, WoundState state)
    {
        if (!_net.IsServer || !Resolve(wound, ref wound.Comp, false) || wound.Comp.State == state)
            return false;

        var old = wound.Comp.State;
        wound.Comp.State = state;
        Dirty(wound);
        var changed = new WoundStateChangedEvent(wound.Comp.HoldingPart, wound, old, state);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref changed);
        RaiseLocalEvent(wound, ref changed);
        return true;
    }

    public bool CloseWound(Entity<WoundComponent?> wound) => SetWoundState(wound, WoundState.Closed);

    public bool RemoveWound(Entity<WoundComponent?> wound)
    {
        if (!_net.IsServer || !Resolve(wound, ref wound.Comp, false) || HasComp<WoundScarComponent>(wound))
            return false;

        return RemoveWoundInternal((wound.Owner, wound.Comp));
    }

    private bool RemoveWoundInternal(Entity<WoundComponent> wound)
    {
        if (TryComp(wound.Comp.HoldingPart, out WoundableComponent? part))
            _containers.Remove(wound.Owner, part.WoundsContainer);
        var removed = new WoundRemovedEvent(wound.Comp.HoldingPart, wound, wound.Comp.Prototype);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref removed);
        RaiseLocalEvent(wound, ref removed);
        QueueDel(wound);
        return true;
    }

    public void ClearWounds(Entity<WoundableComponent?> part)
    {
        if (!_net.IsServer || !Resolve(part, ref part.Comp, false))
            return;

        foreach (var wound in part.Comp.WoundsContainer.ContainedEntities.ToArray())
            if (TryComp(wound, out WoundComponent? component))
                RemoveWoundInternal((wound, component));
    }

    private void HealWounds(Entity<WoundableComponent> part, WoundPrototype prototype, FixedPoint2 amount)
    {
        var remaining = amount * prototype.HealingMultiplier;
        foreach (var wound in GetWounds(part.Owner).ToArray())
        {
            if (remaining <= FixedPoint2.Zero || wound.Comp.Prototype != prototype.ID)
                continue;

            var healed = FixedPoint2.Min(remaining, wound.Comp.Severity);
            ChangeSeverity(wound.Owner, -healed);
            remaining -= healed;
        }
    }

    private bool TryResolvePrototype(ProtoId<DamageTypePrototype> type, out WoundPrototype prototype) =>
        _woundsByDamageType.TryGetValue(type, out prototype!);
}

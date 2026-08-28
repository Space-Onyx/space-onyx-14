using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._Onyx.Chemistry.Circulation;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundBleedingSystem : EntitySystem
{
    private static readonly ProtoId<WoundPrototype> SystemicBleedingWound = "SystemicBleedingWound";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private CirculatoryStreamSystem _circulation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundBleedingComponent, ComponentInit>(OnBleedingInit);
        SubscribeLocalEvent<WoundBleedingComponent, ComponentShutdown>(OnBleedingShutdown);
        SubscribeLocalEvent<WoundBleedingComponent, WoundCreatedEvent>(OnWoundCreated);
        SubscribeLocalEvent<WoundBleedingComponent, WoundChangedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundBleedingComponent, WoundStateChangedEvent>(OnWoundStateChanged);
        SubscribeLocalEvent<WoundBleedingComponent, WoundRemovedEvent>(OnWoundRemoved);
        SubscribeLocalEvent<WoundHostComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    private void OnBleedingShutdown(Entity<WoundBleedingComponent> wound, ref ComponentShutdown args)
    {
        if (TryComp(wound, out WoundComponent? core))
            RefreshBodyForPart(core.HoldingPart);
    }

    private void OnBleedingInit(Entity<WoundBleedingComponent> wound, ref ComponentInit args) => RestartAutomaticClotting(wound);
    private void OnWoundCreated(Entity<WoundBleedingComponent> wound, ref WoundCreatedEvent args) => RestartAutomaticClotting(wound);
    private void OnWoundChanged(Entity<WoundBleedingComponent> wound, ref WoundChangedEvent args)
    {
        if (args.Severity > args.OldSeverity)
        {
            wound.Comp.BleedingSeverity += args.Severity - args.OldSeverity;
            wound.Comp.Treatment = BleedingTreatment.None;
            RestartAutomaticClotting(wound);
        }
        else
            RecomputeAutomaticClotting(wound);
    }

    private void OnWoundStateChanged(Entity<WoundBleedingComponent> wound, ref WoundStateChangedEvent args)
    {
        if (args.State == WoundState.Open)
            RestartAutomaticClotting(wound);
        else
        {
            wound.Comp.AutomaticClottingStartedAt = null;
            wound.Comp.AutomaticClottingAt = null;
            RefreshWound(wound);
        }
    }

    private void OnWoundRemoved(Entity<WoundBleedingComponent> wound, ref WoundRemovedEvent args) => RefreshBodyForPart(args.Part);

    private void OnSleepStateChanged(Entity<WoundHostComponent> body, ref SleepStateChangedEvent args)
    {
        foreach (var wound in GetAttachedBleedingWounds(body))
            RefreshWound((wound.Owner, wound.Comp2), false);
        RefreshBody(body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WoundBleedingComponent, WoundComponent>();
        while (query.MoveNext(out var uid, out var bleeding, out var wound))
        {
            if (bleeding.AutomaticClottingAt is not { } deadline || _timing.CurTime < deadline)
                continue;

            bleeding.AutomaticClottingStartedAt = null;
            bleeding.AutomaticClottingAt = null;
            bleeding.NaturalClotting = Math.Max(bleeding.NaturalClotting, bleeding.BaseRate);
            if (_prototypes.TryIndex(wound.Prototype, out var prototype))
                RefreshWound((uid, bleeding), wound, prototype);
        }
    }

    public bool SetTreatment(Entity<WoundComponent?> wound, BleedingTreatment treatment)
    {
        if (!_net.IsServer || !Resolve(wound, ref wound.Comp, false) ||
            !TryComp(wound, out WoundBleedingComponent? bleeding))
            return false;

        bleeding.Treatment = treatment;
        RefreshWound((wound, bleeding));
        return true;
    }

    public bool ReduceBleeding(Entity<WoundComponent?> wound, FixedPoint2 amount)
    {
        if (!_net.IsServer || amount <= FixedPoint2.Zero || !Resolve(wound, ref wound.Comp, false) ||
            !TryComp(wound, out WoundBleedingComponent? bleeding))
            return false;

        bleeding.BleedingSeverity = FixedPoint2.Max(FixedPoint2.Zero, bleeding.BleedingSeverity - amount);
        if (bleeding.BleedingSeverity == FixedPoint2.Zero)
        {
            RemComp<WoundBleedingComponent>(wound.Owner);
            RefreshBodyForPart(wound.Comp.HoldingPart);
            return true;
        }

        bleeding.Treatment = BleedingTreatment.None;
        RefreshWound((wound, bleeding));
        return true;
    }

    public bool ModifyBodyBleeding(EntityUid body, float amount)
    {
        if (!_net.IsServer || amount == 0f || !HasComp<WoundHostComponent>(body))
            return false;

        if (amount > 0f)
        {
            foreach (var wound in GetAttachedBleedingWounds(body))
            {
                if (wound.Comp1.Prototype != SystemicBleedingWound)
                    continue;

                return _wounds.CreateOrMergeWound(wound.Comp1.HoldingPart,
                    SystemicBleedingWound,
                    FixedPoint2.New(amount)) != null;
            }

            foreach (var (part, _) in _body.GetBodyChildren(body))
            {
                if (_wounds.CanBleed(part) && _wounds.CanCreateWound(part, SystemicBleedingWound))
                {
                    return _wounds.CreateOrMergeWound(part, SystemicBleedingWound, FixedPoint2.New(amount)) != null;
                }
            }

            return false;
        }

        var wounds = GetAttachedBleedingWounds(body)
            .Where(wound => wound.Comp2.CurrentRate > 0f)
            .OrderByDescending(wound => wound.Comp2.CurrentRate)
            .ToArray();
        var remaining = FixedPoint2.New(-amount);
        var modified = false;
        foreach (var wound in wounds)
        {
            var rate = FixedPoint2.New(wound.Comp2.CurrentRate);
            var reduction = remaining >= rate
                ? wound.Comp2.BleedingSeverity
                : wound.Comp2.BleedingSeverity * remaining /
                  FixedPoint2.New(wound.Comp2.CurrentRate + wound.Comp2.NaturalClotting);
            if (reduction <= FixedPoint2.Zero || !ReduceBleeding(wound.Owner, reduction))
                continue;

            modified = true;
            remaining -= FixedPoint2.Min(remaining, rate);
            if (remaining <= FixedPoint2.Zero)
                break;
        }

        return modified;
    }

    public bool StopBodyBleeding(EntityUid body)
    {
        if (!_net.IsServer || !HasComp<WoundHostComponent>(body))
            return false;

        var modified = false;
        foreach (var wound in GetAttachedBleedingWounds(body).ToArray())
            modified |= ReduceBleeding(wound.Owner, wound.Comp2.BleedingSeverity);

        return modified;
    }

    public bool ReducePartBleeding(Entity<WoundableComponent?> part, FixedPoint2 amount)
    {
        if (!_net.IsServer || amount <= FixedPoint2.Zero || !Resolve(part, ref part.Comp, false))
            return false;

        var wounds = _wounds.GetWounds(part)
            .Select(wound => (Wound: wound, Bleeding: CompOrNull<WoundBleedingComponent>(wound)))
            .Where(entry => entry.Bleeding is { CurrentRate: > 0f })
            .OrderByDescending(entry => entry.Bleeding!.CurrentRate)
            .ToArray();
        var remaining = amount;
        var modified = false;
        foreach (var (wound, bleeding) in wounds)
        {
            var rate = FixedPoint2.New(bleeding!.CurrentRate);
            var reduction = remaining >= rate
                ? bleeding.BleedingSeverity
                : bleeding.BleedingSeverity * remaining /
                  FixedPoint2.New(bleeding.CurrentRate + bleeding.NaturalClotting);
            if (reduction <= FixedPoint2.Zero || !ReduceBleeding(wound.Owner, reduction))
                continue;

            modified = true;
            remaining -= FixedPoint2.Min(remaining, rate);
            if (remaining <= FixedPoint2.Zero)
                break;
        }

        return modified;
    }

    public int TreatPart(Entity<WoundableComponent?> part, BleedingTreatment treatment,
        ProtoId<WoundPrototype>? prototype = null)
    {
        if (!Resolve(part, ref part.Comp, false))
            return 0;

        var treated = 0;
        foreach (var wound in _wounds.GetWounds(part).ToArray())
        {
            if ((prototype == null || wound.Comp.Prototype == prototype) && SetTreatment(wound.Owner, treatment))
                treated++;
        }

        return treated;
    }

    public float GetPartRate(Entity<WoundableComponent?> part)
    {
        if (!Resolve(part, ref part.Comp, false))
            return 0f;

        var rate = 0f;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (TryComp(wound, out WoundBleedingComponent? bleeding))
                rate += bleeding.CurrentRate;
        }

        return rate;
    }

    public void OnPartChanged(EntityUid body) => RefreshBody(body);

    public void OnPartInserted(EntityUid part, EntityUid body) => RefreshBody(body, part);

    public void RefreshBody(EntityUid body, EntityUid? insertedPart = null)
    {
        if (!_net.IsServer || !HasComp<WoundHostComponent>(body) ||
            !TryComp(body, out BloodstreamComponent? bloodstream))
            return;

        var partRates = new Dictionary<EntityUid, float>();
        var streamRates = new Dictionary<ProtoId<CirculatoryStreamPrototype>, float>();
        foreach (var wound in GetAttachedBleedingWounds(body))
        {
            var part = wound.Comp1.HoldingPart;
            partRates[part] = partRates.GetValueOrDefault(part) + wound.Comp2.CurrentRate;
            if (TryComp(part, out WoundableComponent? woundable))
            {
                var stream = _circulation.GetPartStream((part, woundable));
                streamRates[stream] = streamRates.GetValueOrDefault(stream) + wound.Comp2.CurrentRate;
            }
        }

        if (insertedPart is { } inserted && TryComp(inserted, out WoundableComponent? insertedWoundable) &&
            !partRates.ContainsKey(inserted))
        {
            partRates[inserted] = 0f;
            foreach (var wound in _wounds.GetWounds((inserted, insertedWoundable)))
                if (TryComp(wound, out WoundBleedingComponent? bleeding))
                {
                    partRates[inserted] += bleeding.CurrentRate;
                    var stream = _circulation.GetPartStream((inserted, insertedWoundable));
                    streamRates[stream] = streamRates.GetValueOrDefault(stream) + bleeding.CurrentRate;
                }
        }

        _circulation.SetBleedRates(body, streamRates);
        foreach (var (part, partRate) in partRates)
        {
            var partChanged = new PartBleedingChangedEvent(body, part, partRate);
            RaiseLocalEvent(part, ref partChanged);
        }

    }

    public void RefreshWound(Entity<WoundBleedingComponent> wound, bool refreshBody = true)
    {
        if (!_net.IsServer || !TryComp(wound, out WoundComponent? core) ||
            !_prototypes.TryIndex(core.Prototype, out var prototype))
            return;

        RefreshWound(wound, core, prototype, refreshBody);
    }

    private void RefreshWound(Entity<WoundBleedingComponent> wound,
        WoundComponent core,
        WoundPrototype prototype,
        bool refreshBody = true)
    {
        if (!_net.IsServer || !prototype.TryGetBehavior(core.Severity, out WoundBleedingBehavior behavior) ||
            core.Severity < behavior.MinimumSeverity ||
            !TryGetBleedingMultiplier(core.HoldingPart, out var bleedingMultiplier))
        {
            wound.Comp.BaseRate = 0f;
            wound.Comp.CurrentRate = 0f;
            Dirty(wound);
            if (refreshBody)
                RefreshBodyForPart(core.HoldingPart);
            return;
        }

        wound.Comp.BaseRate = wound.Comp.BleedingSeverity.Float() * behavior.Rate * bleedingMultiplier;
        if (behavior.AwakeMultiplier > 1f && TryGetBody(core.HoldingPart, out var patient) &&
            !HasComp<SleepingComponent>(patient))
            wound.Comp.BaseRate *= behavior.AwakeMultiplier;
        var multiplier = core.State == WoundState.Open ? TreatmentMultiplier(wound.Comp.Treatment) : 0f;
        wound.Comp.CurrentRate = Math.Max(0f, wound.Comp.BaseRate * multiplier - wound.Comp.NaturalClotting);
        Dirty(wound);

        if (!refreshBody || !TryGetBody(core.HoldingPart, out var body))
            return;

        var changed = new WoundBleedingChangedEvent(body, core.HoldingPart, wound, wound.Comp.CurrentRate);
        RaiseLocalEvent(wound, ref changed);
        RefreshBody(body);
    }

    private bool TryGetBleedingMultiplier(EntityUid part, out float multiplier)
    {
        multiplier = 1f;
        if (!TryComp(part, out WoundableComponent? woundable) ||
            !_prototypes.TryIndex(woundable.Profile, out var profile))
            return true;

        if (profile.BleedingMultiplier <= 0f)
            return false;

        multiplier = profile.BleedingMultiplier;
        return true;
    }

    private void RestartAutomaticClotting(Entity<WoundBleedingComponent> wound)
    {
        wound.Comp.NaturalClotting = 0f;
        wound.Comp.AutomaticClottingStartedAt = _timing.CurTime;
        RecomputeAutomaticClotting(wound);
    }

    private void RecomputeAutomaticClotting(Entity<WoundBleedingComponent> wound)
    {
        if (!_net.IsServer || !TryComp(wound, out WoundComponent? core) ||
            !_prototypes.TryIndex(core.Prototype, out var prototype) ||
            !prototype.TryGetBehavior(core.Severity, out WoundBleedingBehavior behavior))
            return;

        var secondsPerSeverity = _configuration.GetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity);
        var maximum = _configuration.GetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds);
        if (core.State != WoundState.Open || !_configuration.GetCVar(CCVars.WoundsBleedingAutoStopEnabled) ||
            secondsPerSeverity <= 0f || maximum <= 0f || behavior.ClottingMultiplier <= 0f)
        {
            wound.Comp.AutomaticClottingStartedAt = null;
            wound.Comp.AutomaticClottingAt = null;
            RefreshWound(wound, core, prototype);
            return;
        }

        var minimum = Math.Clamp(_configuration.GetCVar(CCVars.WoundsBleedingAutoStopMinSeconds), 0f, maximum);
        var duration = Math.Clamp(core.Severity.Float() * secondsPerSeverity * behavior.ClottingMultiplier,
            minimum,
            maximum);
        wound.Comp.AutomaticClottingStartedAt ??= _timing.CurTime;
        wound.Comp.AutomaticClottingAt = wound.Comp.AutomaticClottingStartedAt + TimeSpan.FromSeconds(duration);

        if (_timing.CurTime >= wound.Comp.AutomaticClottingAt)
        {
            wound.Comp.AutomaticClottingStartedAt = null;
            wound.Comp.AutomaticClottingAt = null;
            var rate = wound.Comp.BleedingSeverity.Float() * behavior.Rate;
            if (behavior.AwakeMultiplier > 1f && TryGetBody(core.HoldingPart, out var patient) &&
                !HasComp<SleepingComponent>(patient))
                rate *= behavior.AwakeMultiplier;
            wound.Comp.NaturalClotting = Math.Max(wound.Comp.NaturalClotting, rate);
        }

        RefreshWound(wound, core, prototype);
    }

    private IEnumerable<Entity<WoundComponent, WoundBleedingComponent>> GetAttachedBleedingWounds(EntityUid body)
    {
        foreach (var (part, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp(part, out WoundableComponent? woundable))
                continue;

            foreach (var wound in _wounds.GetWounds((part, woundable)))
                if (TryComp(wound, out WoundBleedingComponent? bleeding))
                    yield return (wound, wound.Comp, bleeding);
        }
    }

    private void RefreshBodyForPart(EntityUid part)
    {
        if (TryGetBody(part, out var body))
            RefreshBody(body, part);
    }

    private bool TryGetBody(EntityUid part, out EntityUid body)
    {
        body = default;
        if (!TryComp(part, out BodyPartComponent? partComp) || partComp.Body is not { } attachedBody)
            return false;

        body = attachedBody;
        return true;
    }

    private static float TreatmentMultiplier(BleedingTreatment treatment) => treatment switch
    {
        BleedingTreatment.None => 1f,
        BleedingTreatment.Bandaged => 0.25f,
        BleedingTreatment.Clamped => 0f,
        BleedingTreatment.Sutured => 0f,
        BleedingTreatment.Cauterized => 0f,
        _ => 1f,
    };
}

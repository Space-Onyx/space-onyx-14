using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundBleedingSystem : EntitySystem
{
    private static readonly FixedPoint2 StrongBleedingSeverity = 15;
    private static readonly TimeSpan StrongBandageRemovalDelay = TimeSpan.FromSeconds(5);

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundBleedingComponent, ComponentInit>(OnBleedingInit);
        SubscribeLocalEvent<WoundBleedingComponent, WoundCreatedEvent>(OnWoundCreated);
        SubscribeLocalEvent<WoundBleedingComponent, WoundChangedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundBleedingComponent, WoundStateChangedEvent>(OnWoundStateChanged);
        SubscribeLocalEvent<WoundBleedingComponent, WoundRemovedEvent>(OnWoundRemoved);
    }

    private void OnBleedingInit(Entity<WoundBleedingComponent> wound, ref ComponentInit args) => RestartAutomaticClotting(wound);
    private void OnWoundCreated(Entity<WoundBleedingComponent> wound, ref WoundCreatedEvent args) => RestartAutomaticClotting(wound);
    private void OnWoundChanged(Entity<WoundBleedingComponent> wound, ref WoundChangedEvent args)
    {
        if (args.Severity > args.OldSeverity)
            RestartAutomaticClotting(wound);
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WoundBleedingComponent, WoundComponent>();
        while (query.MoveNext(out var uid, out var bleeding, out var wound))
        {
            if (bleeding.BandageRemovalAt is { } removalAt && _timing.CurTime >= removalAt)
            {
                _wounds.RemoveWound((uid, wound));
                continue;
            }

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
        bleeding.BandageRemovalAt = null;
        if (treatment == BleedingTreatment.Bandaged)
        {
            if (wound.Comp.Severity < StrongBleedingSeverity)
                return _wounds.RemoveWound(wound);

            bleeding.BandageRemovalAt = _timing.CurTime + StrongBandageRemovalDelay;
        }

        RefreshWound((wound, bleeding));
        return true;
    }

    public bool TreatMostBleedingWound(Entity<WoundableComponent?> part, BleedingTreatment treatment)
    {
        if (!Resolve(part, ref part.Comp, false))
            return false;

        Entity<WoundComponent, WoundBleedingComponent>? selected = null;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (!TryComp(wound, out WoundBleedingComponent? bleeding) || bleeding.CurrentRate <= 0f ||
                selected is { } current && current.Comp2.CurrentRate >= bleeding.CurrentRate)
                continue;

            selected = (wound, wound.Comp, bleeding);
        }

        return selected is { } target && SetTreatment(target.Owner, treatment);
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
        foreach (var wound in GetAttachedBleedingWounds(body))
            partRates[wound.Comp1.HoldingPart] = partRates.GetValueOrDefault(wound.Comp1.HoldingPart) + wound.Comp2.CurrentRate;

        if (insertedPart is { } inserted && TryComp(inserted, out WoundableComponent? woundable) &&
            !partRates.ContainsKey(inserted))
        {
            foreach (var wound in _wounds.GetWounds((inserted, woundable)))
                if (TryComp(wound, out WoundBleedingComponent? bleeding))
                    partRates[inserted] = partRates.GetValueOrDefault(inserted) + bleeding.CurrentRate;
        }

        var rate = 0f;
        foreach (var partRate in partRates.Values)
            rate += partRate;
        _bloodstream.TryModifyBleedAmount((body, bloodstream), rate - bloodstream.BleedAmount);
        foreach (var (part, partRate) in partRates)
        {
            var partChanged = new PartBleedingChangedEvent(body, part, partRate);
            RaiseLocalEvent(part, ref partChanged);
        }

        var bodyChanged = new BodyBleedingProjectionChangedEvent(body, rate);
        RaiseLocalEvent(body, ref bodyChanged);
    }

    private void RefreshWound(Entity<WoundBleedingComponent> wound, bool refreshBody = true)
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
        if (!_net.IsServer)
            return;

        wound.Comp.BaseRate = core.Severity.Float() * prototype.BleedingRate;
        var multiplier = core.State == WoundState.Open ? TreatmentMultiplier(wound.Comp.Treatment) : 0f;
        wound.Comp.CurrentRate = Math.Max(0f, wound.Comp.BaseRate * multiplier - wound.Comp.NaturalClotting);
        Dirty(wound);

        if (!refreshBody || !TryGetBody(core.HoldingPart, out var body))
            return;

        var changed = new WoundBleedingChangedEvent(body, core.HoldingPart, wound, wound.Comp.CurrentRate);
        RaiseLocalEvent(wound, ref changed);
        RefreshBody(body);
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
            !_prototypes.TryIndex(core.Prototype, out var prototype))
            return;

        var secondsPerSeverity = _configuration.GetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity);
        var maximum = _configuration.GetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds);
        if (core.State != WoundState.Open || !_configuration.GetCVar(CCVars.WoundsBleedingAutoStopEnabled) ||
            secondsPerSeverity <= 0f || maximum <= 0f || prototype.AutomaticClottingTimeMultiplier <= 0f)
        {
            wound.Comp.AutomaticClottingStartedAt = null;
            wound.Comp.AutomaticClottingAt = null;
            RefreshWound(wound, core, prototype);
            return;
        }

        var minimum = Math.Clamp(_configuration.GetCVar(CCVars.WoundsBleedingAutoStopMinSeconds), 0f, maximum);
        var duration = Math.Clamp(core.Severity.Float() * secondsPerSeverity * prototype.AutomaticClottingTimeMultiplier,
            minimum,
            maximum);
        wound.Comp.AutomaticClottingStartedAt ??= _timing.CurTime;
        wound.Comp.AutomaticClottingAt = wound.Comp.AutomaticClottingStartedAt + TimeSpan.FromSeconds(duration);

        if (_timing.CurTime >= wound.Comp.AutomaticClottingAt)
        {
            wound.Comp.AutomaticClottingStartedAt = null;
            wound.Comp.AutomaticClottingAt = null;
            wound.Comp.NaturalClotting = Math.Max(wound.Comp.NaturalClotting, core.Severity.Float() * prototype.BleedingRate);
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
            RefreshBody(body);
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

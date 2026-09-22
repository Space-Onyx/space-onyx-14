using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Overlays;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Overlays;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Cybernetics;

public sealed partial class CyberneticsSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberneticsComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CyberneticsComponent, EmpAttemptEvent>(OnEmpAttempt);
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemovedEvent>(OnEmpRemoved);
        SubscribeLocalEvent<BodyComponent, EmpPulseEvent>(OnBodyEmpPulse);
        SubscribeLocalEvent<BodyComponent, BodyOrgansChangedEvent>(OnBodyOrgansChanged);
    }

    private void OnRemoved(Entity<CyberneticsComponent> ent, ref OrganGotRemovedEvent args)
    {
        CaptureVisionState(ent, args.Target);
    }

    private void OnEmpAttempt(Entity<CyberneticsComponent> ent, ref EmpAttemptEvent args)
    {
        if (TryGetBody(ent, out _))
            args.Cancelled = true;
    }

    private void OnEmpPulse(Entity<CyberneticsComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Disabled)
        {
            args.Affected = true;
            args.Disabled = true;
            return;
        }

        args.Affected = true;
        args.Disabled = true;
        if (TryGetBody(ent, out var body))
            CaptureVisionState(ent, body);

        ent.Comp.Disabled = true;
        Dirty(ent);

        if (TryGetBody(ent, out body))
        {
            NotifyOrganFunctionChanged(ent, body);
            if (_net.IsServer && HasComp<BodyPartComponent>(ent))
                _functionality.RefreshPart(body, ent);
        }

        if (_net.IsServer && HasComp<BodyPartComponent>(ent))
        {
            var shock = new DamageSpecifier(_prototypes.Index(Shock), 30);
            _damage.TryChangeDamage(ent.Owner, shock, true);
        }
    }

    private void OnEmpRemoved(Entity<CyberneticsComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        if (!ent.Comp.Disabled)
            return;

        ent.Comp.Disabled = false;
        Dirty(ent);
        if (TryGetBody(ent, out var body))
        {
            NotifyOrganFunctionChanged(ent, body);
            if (_net.IsServer && HasComp<BodyPartComponent>(ent))
                _functionality.RefreshPart(body, ent);
        }
    }

    private void OnBodyEmpPulse(Entity<BodyComponent> ent, ref EmpPulseEvent args)
    {
        foreach (var (part, _) in _body.GetBodyChildren(ent))
        {
            if (HasComp<CyberneticsComponent>(part))
                TryRelayEmp(ent, part, args);
        }

        foreach (var (organ, _) in _body.GetBodyOrgans(ent))
        {
            if (HasComp<CyberneticsComponent>(organ))
                TryRelayEmp(ent, organ, args);
        }
    }

    private void OnBodyOrgansChanged(Entity<BodyComponent> ent, ref BodyOrgansChangedEvent args)
    {
        if (_net.IsClient)
            return;

        var nightVisionEnabled = false;
        var thermalVisionEnabled = false;

        foreach (var (partId, _) in _body.GetBodyChildren(ent))
        {
            CollectVisionState(partId, ref nightVisionEnabled, ref thermalVisionEnabled);
            foreach (var slot in Comp<BodyPartComponent>(partId).Organs)
            {
                if (_body.TryGetOrganInSlot(partId, slot, out var organ))
                    CollectVisionState(organ, ref nightVisionEnabled, ref thermalVisionEnabled);
            }
        }

        if (TryComp(ent, out NightVisionComponent? nightVision) && nightVision.Enabled != nightVisionEnabled)
        {
            nightVision.Enabled = nightVisionEnabled;
            Dirty(ent, nightVision);
        }

        if (TryComp(ent, out ThermalVisionComponent? thermalVision) && thermalVision.Enabled != thermalVisionEnabled)
        {
            thermalVision.Enabled = thermalVisionEnabled;
            Dirty(ent, thermalVision);
        }
    }

    private void TryRelayEmp(EntityUid body, EntityUid cybernetic, EmpPulseEvent args)
    {
        var protection = new CyberneticsEmpProtectionEvent(cybernetic);
        RaiseLocalEvent(body, ref protection);
        if (protection.Cancelled || protection.StrengthMultiplier <= 0f || protection.DurationMultiplier <= 0f)
            return;

        _emp.DoEmpEffects(
            cybernetic,
            args.EnergyConsumption * protection.StrengthMultiplier,
            args.Duration * protection.DurationMultiplier,
            args.User);
    }

    private bool TryGetBody(EntityUid uid, out EntityUid body)
    {
        if (TryComp(uid, out BodyPartComponent? part) && part.Body is { } partBody)
        {
            body = partBody;
            return true;
        }

        if (TryComp(uid, out OrganComponent? organ) && organ.Body is { } organBody)
        {
            body = organBody;
            return true;
        }

        body = default;
        return false;
    }

    private void CollectVisionState(
        EntityUid uid,
        ref bool nightVisionEnabled,
        ref bool thermalVisionEnabled)
    {
        if (!TryComp(uid, out CyberneticsComponent? cyber) || cyber.Disabled)
            return;

        if (ProvidesVision(uid, "NightVision"))
            nightVisionEnabled = cyber.NightVisionEnabled;
        if (ProvidesVision(uid, "ThermalVision"))
            thermalVisionEnabled = cyber.ThermalVisionEnabled;
    }

    private bool ProvidesVision(EntityUid uid, string componentName)
    {
        return TryComp(uid, out FunctionalOrganComponent? functional) &&
            functional.Components.ContainsKey(componentName);
    }

    private void NotifyOrganFunctionChanged(EntityUid organ, EntityUid body)
    {
        var changed = new OrganFunctionChangedEvent(body, false);
        RaiseLocalEvent(organ, ref changed);
    }

    /// <summary>
    /// Temporarily disables cybernetics without applying EMP energy or damage.
    /// </summary>
    public bool TryDisable(Entity<CyberneticsComponent?> ent, TimeSpan duration)
    {
        if (!Resolve(ent, ref ent.Comp, false) || duration <= TimeSpan.Zero)
            return false;

        ent.Comp.Disabled = true;
        Dirty(ent);
        var disabled = EnsureComp<EmpDisabledComponent>(ent);
        var disabledUntil = _timing.CurTime + duration;
        if (disabled.DisabledUntil < disabledUntil)
            disabled.DisabledUntil = disabledUntil;
        Dirty(ent.Owner, disabled);
        if (TryGetBody(ent, out var body))
        {
            NotifyOrganFunctionChanged(ent, body);
            if (_net.IsServer && HasComp<BodyPartComponent>(ent))
                _functionality.RefreshPart(body, ent);
        }
        return true;
    }

    private void CaptureVisionState(Entity<CyberneticsComponent> cybernetic, EntityUid body)
    {
        if (ProvidesVision(cybernetic.Owner, "NightVision") &&
            TryComp(body, out NightVisionComponent? nightVision))
            cybernetic.Comp.NightVisionEnabled = nightVision.Enabled;
        if (ProvidesVision(cybernetic.Owner, "ThermalVision") &&
            TryComp(body, out ThermalVisionComponent? thermalVision))
            cybernetic.Comp.ThermalVisionEnabled = thermalVision.Enabled;
        Dirty(cybernetic);
    }
}

/// <summary>
/// Raised on a body before an EMP is relayed to its installed cybernetics.
/// Protection can cancel the relay or reduce its strength and disable duration.
/// </summary>
[ByRefEvent]
public record struct CyberneticsEmpProtectionEvent(
    EntityUid Cybernetic,
    bool Cancelled = false,
    float StrengthMultiplier = 1f,
    float DurationMultiplier = 1f);

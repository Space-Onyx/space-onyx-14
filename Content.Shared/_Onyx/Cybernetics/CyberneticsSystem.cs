using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Flash.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared._Onyx.Overlays;
using Content.Shared._Onyx.Shadowkin;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;
using Content.Shared.Prying.Components;
using Content.Shared.Contraband;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Cybernetics;

public sealed partial class CyberneticsSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberneticsComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<CyberneticsComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemovedEvent>(OnEmpRemoved);
        SubscribeLocalEvent<BodyComponent, EmpPulseEvent>(OnBodyEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, NeuroBandwidthEfficiencyChangedEvent>(OnNeuroEfficiencyChanged);
        SubscribeLocalEvent<CyberneticBodyEffectsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnInserted(Entity<CyberneticsComponent> ent, ref OrganGotInsertedEvent args)
    {
        RefreshBody(args.Target);
    }

    private void OnRemoved(Entity<CyberneticsComponent> ent, ref OrganGotRemovedEvent args)
    {
        CaptureVisionState(ent, args.Target);
        RefreshBody(args.Target);
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
            RefreshBody(body);
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
            RefreshBody(body);
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

    private void OnNeuroEfficiencyChanged(Entity<CyberneticsComponent> ent, ref NeuroBandwidthEfficiencyChangedEvent args)
    {
        if (TryGetBody(ent, out var body))
            RefreshBody(body);
    }

    private void TryRelayEmp(EntityUid body, EntityUid cybernetic, EmpPulseEvent args)
    {
        var protection = new CyberneticsEmpProtectionEvent(cybernetic);
        RaiseLocalEvent(body, ref protection);
        if (protection.Cancelled || protection.StrengthMultiplier <= 0f || protection.DurationMultiplier <= 0f)
            return;

        _emp.TryEmpEffects(
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

    public void RefreshBody(EntityUid body)
    {
        if (_net.IsClient || TerminatingOrDeleted(body))
            return;

        var effects = CyberneticEffect.None;
        var speedLegs = 0;
        var nightVisionEnabled = false;
        var thermalVisionEnabled = false;

        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            AddEffects(partId, ref effects, ref speedLegs, ref nightVisionEnabled, ref thermalVisionEnabled);
            foreach (var slot in Comp<BodyPartComponent>(partId).Organs)
                if (_body.TryGetOrganInSlot(partId, slot, out var organ))
                    AddEffects(organ, ref effects, ref speedLegs, ref nightVisionEnabled, ref thermalVisionEnabled);
        }

        var state = EnsureComp<CyberneticBodyEffectsComponent>(body);
        state.SpeedLegs = speedLegs;
        SetOwned<PryingComponent>(body, effects.HasFlag(CyberneticEffect.Prying), ref state.OwnsPrying, component =>
        {
            component.PryPowered = true;
            component.SpeedModifier = 1.5f;
        });
        SetOwned<ShowHealthBarsComponent>(body,
            effects.HasFlag(CyberneticEffect.MedicalHud) || effects.HasFlag(CyberneticEffect.DiagnosticHud),
            ref state.OwnsHealthBars);
        SetOwned<ShowHealthIconsComponent>(body,
            effects.HasFlag(CyberneticEffect.MedicalHud),
            ref state.OwnsHealthIcons);
        if (state.OwnsHealthBars && TryComp(body, out ShowHealthBarsComponent? bars))
        {
            SetHudContainers(bars.DamageContainers, effects);
            Dirty(body, bars);
        }
        if (state.OwnsHealthIcons && TryComp(body, out ShowHealthIconsComponent? icons))
        {
            SetHudContainers(icons.DamageContainers, effects);
            Dirty(body, icons);
        }
        SetOwned<ShowJobIconsComponent>(body, effects.HasFlag(CyberneticEffect.SecurityHud), ref state.OwnsJobIcons);
        SetOwned<ShowMindShieldIconsComponent>(body, effects.HasFlag(CyberneticEffect.SecurityHud), ref state.OwnsMindShieldIcons);
        SetOwned<ShowCriminalRecordIconsComponent>(body, effects.HasFlag(CyberneticEffect.SecurityHud), ref state.OwnsCriminalRecordIcons);
        SetOwned<ShowSquadIconsComponent>(body, effects.HasFlag(CyberneticEffect.SecurityHud), ref state.OwnsSquadIcons);
        SetOwned<ShowContrabandDetailsComponent>(body, effects.HasFlag(CyberneticEffect.SecurityHud), ref state.OwnsContrabandDetails);
        SetOwned<ShowAccessReaderSettingsComponent>(body,
            effects.HasFlag(CyberneticEffect.DiagnosticHud),
            ref state.OwnsAccessReaderSettings);
        SetOwned<FlashImmunityComponent>(body,
            effects.HasFlag(CyberneticEffect.FlashProtection),
            ref state.OwnsFlashImmunity);
        if (!effects.HasFlag(CyberneticEffect.NightVision) && state.OwnsNightVision)
            TransferNightVisionOwnership(body, state);
        SetOwned<NightVisionComponent>(body,
            effects.HasFlag(CyberneticEffect.NightVision),
            ref state.OwnsNightVision,
            component =>
            {
                component.Enabled = nightVisionEnabled;
                component.Action = "ActionToggleNightVision";
            });
        if (state.OwnsNightVision && TryComp(body, out NightVisionComponent? nightVision) && nightVision.ActionEntity == null)
        {
            _actions.AddAction(body, ref nightVision.ActionEntity, nightVision.Action);
            Dirty(body, nightVision);
        }
        SetOwned<ThermalVisionComponent>(body,
            effects.HasFlag(CyberneticEffect.ThermalVision),
            ref state.OwnsThermalVision,
            component => component.Enabled = thermalVisionEnabled);
        EntityManager.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(body);
    }

    private static void SetHudContainers(List<ProtoId<DamageContainerPrototype>> containers, CyberneticEffect effects)
    {
        containers.Clear();
        if (effects.HasFlag(CyberneticEffect.MedicalHud))
            containers.Add("Biological");
        if (effects.HasFlag(CyberneticEffect.DiagnosticHud))
        {
            containers.Add("Inorganic");
            containers.Add("Silicon");
        }
    }

    private void AddEffects(
        EntityUid uid,
        ref CyberneticEffect effects,
        ref int speedLegs,
        ref bool nightVisionEnabled,
        ref bool thermalVisionEnabled)
    {
        if (!TryComp(uid, out CyberneticsComponent? cyber) || cyber.Disabled ||
            TryComp(uid, out NeuroBandwidthRuntimeComponent? runtime) && runtime.Efficiency <= 0f)
            return;

        effects |= cyber.Effects;
        if (cyber.Effects.HasFlag(CyberneticEffect.NightVision))
            nightVisionEnabled = cyber.NightVisionEnabled;
        if (cyber.Effects.HasFlag(CyberneticEffect.ThermalVision))
            thermalVisionEnabled = cyber.ThermalVisionEnabled;
        if (cyber.Effects.HasFlag(CyberneticEffect.Speed))
            speedLegs++;
    }

    private void CaptureVisionState(Entity<CyberneticsComponent> cybernetic, EntityUid body)
    {
        if (cybernetic.Comp.Effects.HasFlag(CyberneticEffect.NightVision) &&
            TryComp(body, out NightVisionComponent? nightVision))
            cybernetic.Comp.NightVisionEnabled = nightVision.Enabled;
        if (cybernetic.Comp.Effects.HasFlag(CyberneticEffect.ThermalVision) &&
            TryComp(body, out ThermalVisionComponent? thermalVision))
            cybernetic.Comp.ThermalVisionEnabled = thermalVision.Enabled;
        Dirty(cybernetic);
    }

    private void TransferNightVisionOwnership(EntityUid body, CyberneticBodyEffectsComponent state)
    {
        foreach (var (partId, part) in _body.GetBodyChildren(body))
        {
            if (TryTransferNightVision(partId, state))
                return;
            foreach (var slot in part.Organs)
            {
                if (_body.TryGetOrganInSlot(partId, slot, out var organ) && TryTransferNightVision(organ, state))
                    return;
            }
        }
    }

    private bool TryTransferNightVision(EntityUid organ, CyberneticBodyEffectsComponent state)
    {
        if (!TryComp(organ, out ShadowkinEyesComponent? eyes))
            return false;
        eyes.GrantedNightVision = true;
        state.OwnsNightVision = false;
        Dirty(organ, eyes);
        return true;
    }

    private void SetOwned<T>(EntityUid body, bool enabled, ref bool owned, Action<T>? configure = null)
        where T : Component, new()
    {
        if (enabled)
        {
            if (!HasComp<T>(body))
            {
                var component = EnsureComp<T>(body);
                configure?.Invoke(component);
                Dirty(body, component);
                owned = true;
            }
            return;
        }

        if (!owned)
            return;

        RemComp<T>(body);
        owned = false;
    }

    private void OnRefreshSpeed(Entity<CyberneticBodyEffectsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.SpeedLegs > 0)
        {
            var multiplier = MathF.Pow(1.075f, ent.Comp.SpeedLegs);
            args.ModifySpeed(multiplier);
        }
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

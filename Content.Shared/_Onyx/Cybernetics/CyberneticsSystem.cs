using Content.Shared.Access.Components;
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
using Content.Shared.Prying.Components;
using Content.Shared.Contraband;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Cybernetics;

public sealed partial class CyberneticsSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberneticsComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<CyberneticsComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemovedEvent>(OnEmpRemoved);
        SubscribeLocalEvent<CyberneticBodyEffectsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnInserted(Entity<CyberneticsComponent> ent, ref OrganGotInsertedEvent args)
    {
        RefreshBody(args.Target);
    }

    private void OnRemoved(Entity<CyberneticsComponent> ent, ref OrganGotRemovedEvent args)
    {
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
        ent.Comp.Disabled = true;
        Dirty(ent);

        if (TryGetBody(ent, out var body))
            RefreshBody(body);

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
            RefreshBody(body);
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

        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            AddEffects(partId, ref effects, ref speedLegs);
            foreach (var slot in Comp<BodyPartComponent>(partId).Organs)
                if (_body.TryGetOrganInSlot(partId, slot, out var organ))
                    AddEffects(organ, ref effects, ref speedLegs);
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

    private void AddEffects(EntityUid uid, ref CyberneticEffect effects, ref int speedLegs)
    {
        if (!TryComp(uid, out CyberneticsComponent? cyber) || cyber.Disabled)
            return;

        effects |= cyber.Effects;
        if (cyber.Effects.HasFlag(CyberneticEffect.Speed))
            speedLegs++;
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

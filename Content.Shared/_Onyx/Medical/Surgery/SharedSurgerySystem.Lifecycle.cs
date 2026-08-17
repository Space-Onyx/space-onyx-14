using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.GameTicking;
using Content.Shared.Standing;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
        SubscribeLocalEvent<BodyPartComponent, ComponentShutdown>(OnBodyPartShutdown);
        SubscribeLocalEvent<SurgeryTargetComponent, ComponentStartup>(OnSurgeryTargetStartup);
        SubscribeLocalEvent<SurgeryTargetComponent, ComponentShutdown>(OnSurgeryTargetShutdown);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryMarkerConditionComponent, SurgeryValidEvent>(OnMarkerConditionValid);
        SubscribeLocalEvent<SurgerySpeciesConditionComponent, SurgeryValidEvent>(OnSpeciesConditionValid);
        SubscribeLocalEvent<SurgeryOrganTagConditionComponent, SurgeryValidEvent>(OnOrganTagConditionValid);
        SubscribeLocalEvent<SurgeryOrganTagConditionComponent, SurgeryCanPerformStepEvent>(OnOrganTagConditionCanPerform);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryMissingPartConditionComponent, SurgeryValidEvent>(OnMissingPartConditionValid);
        SubscribeLocalEvent<SurgeryDetachablePartConditionComponent, SurgeryValidEvent>(OnDetachablePartConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryValidEvent>(OnOrganHealValid);
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryStepEvent>(OnOrganHeal);
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryStepCompleteCheckEvent>(OnOrganHealCheck);
        SubscribeLocalEvent<SurgeryCavityConditionComponent, SurgeryValidEvent>(OnCavityConditionValid);
        SubscribeLocalEvent<SurgeryPacificationConditionComponent, SurgeryValidEvent>(OnPacificationConditionValid);
        SubscribeLocalEvent<SurgeryPacificationEffectComponent, SurgeryStepEvent>(OnPacificationEffect);
        SubscribeLocalEvent<SurgeryPacificationEffectComponent, SurgeryStepCompleteCheckEvent>(OnPacificationEffectCheck);
        SubscribeLocalEvent<SurgeryMutingConditionComponent, SurgeryValidEvent>(OnMutingConditionValid);
        SubscribeLocalEvent<SurgeryMutingEffectComponent, SurgeryStepEvent>(OnMutingEffect);
        SubscribeLocalEvent<SurgeryMutingEffectComponent, SurgeryStepCompleteCheckEvent>(OnMutingEffectCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnToolStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteCheckEvent>(OnToolCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnToolCanPerform);
        SubscribeLocalEvent<SurgeryStepPainInflicterComponent, SurgeryStepEvent>(OnPainInflicterStep);
        SubscribeLocalEvent<SurgeryDetachPartEffectComponent, SurgeryStepEvent>(OnDetachPart);
        SubscribeLocalEvent<SurgeryDetachPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnDetachPartCheck);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryStepEvent>(OnAttachPart);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnAttachPartCheck);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryCanPerformStepEvent>(OnAttachPartCanPerform);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryGetStepSequenceContextEvent>(OnAttachPartGetSequenceContext);
        SubscribeLocalEvent<SurgeryMendAttachedPartEffectComponent, SurgeryStepEvent>(OnMendAttachedPart);
        SubscribeLocalEvent<SurgeryMendAttachedPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnMendAttachedPartCheck);
        SubscribeLocalEvent<SurgerySutureAttachedPartEffectComponent, SurgeryStepEvent>(OnSutureAttachedPart);
        SubscribeLocalEvent<SurgerySutureAttachedPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnSutureAttachedPartCheck);
        SubscribeLocalEvent<SurgeryRemoveOrganEffectComponent, SurgeryStepEvent>(OnRemoveOrgan);
        SubscribeLocalEvent<SurgeryRemoveOrganEffectComponent, SurgeryStepCompleteCheckEvent>(OnRemoveOrganCheck);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryStepEvent>(OnInsertOrgan);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryStepCompleteCheckEvent>(OnInsertOrganCheck);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryCanPerformStepEvent>(OnInsertOrganCanPerform);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryGetStepSequenceContextEvent>(OnInsertOrganGetSequenceContext);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryStepEvent>(OnInsertCavityItem);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryStepCompleteCheckEvent>(OnInsertCavityItemCheck);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryCanPerformStepEvent>(OnInsertCavityItemCanPerform);
        SubscribeLocalEvent<SurgeryRemoveCavityItemEffectComponent, SurgeryStepEvent>(OnRemoveCavityItem);
        SubscribeLocalEvent<SurgeryRemoveCavityItemEffectComponent, SurgeryStepCompleteCheckEvent>(OnRemoveCavityItemCheck);
        SubscribeLocalEvent<SurgeryTargetComponent, StandAttemptEvent>(OnTargetStandAttempt);

        LoadSurgeryPrototypes();
    }

    private void OnBodyPartStartup(Entity<BodyPartComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<SurgeryTargetComponent>(ent);
    }

    private void OnSurgeryTargetStartup(Entity<SurgeryTargetComponent> ent, ref ComponentStartup args)
    {
        var ui = EnsureComp<UserInterfaceComponent>(ent);
        _ui.SetUi((ent.Owner, ui), SurgeryUIKey.Key, new InterfaceData("SurgeryBoundUserInterface"));
    }

    private void OnBodyPartShutdown(Entity<BodyPartComponent> ent, ref ComponentShutdown args)
    {
        RemoveSurgerySites(ent.Owner);
    }

    private void OnSurgeryTargetShutdown(Entity<SurgeryTargetComponent> ent, ref ComponentShutdown args)
    {
        RemoveSurgerySites(ent.Owner);
    }

    private void RemoveSurgerySites(EntityUid entity)
    {
        foreach (var site in ActiveSurgerySites.Keys.ToArray())
        {
            if (site.Body == entity || site.Part == entity)
                ActiveSurgerySites.Remove(site);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _singletons.Clear();
        ActiveSurgerySites.Clear();
    }

    protected virtual void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        ClearSingletons();
        LoadSurgeryPrototypes();
    }

    protected void ClearSingletons()
    {
        foreach (var singleton in _singletons.Values)
            QueueDel(singleton);

        _singletons.Clear();
    }
}

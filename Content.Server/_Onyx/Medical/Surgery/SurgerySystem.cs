using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundScarSystem _scars = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SurgeryInfectionSystem _infection = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<InteractionVerb>>(OnGetSurgeryVerb);
        SubscribeLocalEvent<SurgeryStepBleedEffectComponent, SurgeryStepEvent>(OnStepBleedComplete);
        SubscribeLocalEvent<SurgeryClampBleedEffectComponent, SurgeryStepEvent>(OnStepClampBleedComplete);
        SubscribeLocalEvent<SurgeryCloseIncisionEffectComponent, SurgeryStepEvent>(OnCloseIncisionComplete);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepEmoteComplete);
        SubscribeLocalEvent<WoundComponent, WoundCreatedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<WoundComponent, WoundChangedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<WoundComponent, WoundRemovedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<WoundComponent, WoundBleedingChangedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<WoundComponent, FractureGradeChangedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<WoundComponent, FractureTreatmentChangedEvent>(OnPatientStateChanged);
        SubscribeLocalEvent<BodyComponent, BodyOrganSlotChangedEvent>(OnBodyOrganSlotChanged);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen);
            subs.Event<SurgeryStepsStateRequest>(OnStepsStateRequest);
        });
    }

    protected override void OnToolStepCompleted(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!HasComp<MechanicalSurgeryStepComponent>(ent))
            _infection.OnStep(ref args);
    }

}

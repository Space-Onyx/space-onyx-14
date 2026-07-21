using System.Linq;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.CCVar;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;
using Content.Shared.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    private static readonly ProtoId<WoundPrototype> SurgicalIncision = "SurgicalIncisionWound";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundScarSystem _scars = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SurgeryInfectionSystem _infection = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly List<EntProtoId> _surgeries = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<InteractionVerb>>(OnGetSurgeryVerb);
        SubscribeLocalEvent<SurgeryStepBleedEffectComponent, SurgeryStepEvent>(OnStepBleedComplete);
        SubscribeLocalEvent<SurgeryClampBleedEffectComponent, SurgeryStepEvent>(OnStepClampBleedComplete);
        SubscribeLocalEvent<SurgeryCloseIncisionEffectComponent, SurgeryStepEvent>(OnCloseIncisionComplete);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepEmoteComplete);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
            subs.Event<SurgeryStepsStateRequest>(OnStepsStateRequest));
        LoadPrototypes();
    }

    protected override void OnToolStepCompleted(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        _infection.OnStep(ref args);
    }

    private void OnStepsStateRequest(Entity<SurgeryTargetComponent> ent, ref SurgeryStepsStateRequest args)
    {
        var part = GetEntity(args.Part);
        if (!TryComp(part, out BodyPartComponent? partComp) ||
            !IsPartOfTarget(ent, part) ||
            GetSingleton(args.Surgery) is not { } surgery ||
            !TryComp(surgery, out SurgeryComponent? surgeryComp))
            return;

        var completed = new List<bool>(surgeryComp.Steps.Count);
        foreach (var step in surgeryComp.Steps)
            completed.Add(IsStepComplete(ent, part, step));

        var next = GetNextStep(ent, part, (surgery, surgeryComp), new List<EntityUid>());
        var nextStep = next is { } value && value.Surgery.Owner == surgery ? value.Step : -1;
        var available = false;
        string? popup = null;
        var reason = StepInvalidReason.None;
        if (nextStep >= 0 && GetSingleton(surgeryComp.Steps[nextStep]) is { } stepEnt)
            available = CanPerformStep(args.Actor, ent, partComp.PartType, stepEnt, false, out popup, out reason, out _);

        _ui.ServerSendUiMessage(ent.Owner, SurgeryUIKey.Key,
            new SurgeryStepsStateResponse(args.Part, args.Surgery, completed, nextStep, available, popup, reason),
            args.Actor);
    }

    protected override void RefreshUI(EntityUid body)
    {
        if (!HasComp<SurgeryTargetComponent>(body))
            return;

        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        var parts = TryComp(body, out BodyPartComponent? rootPart)
            ? _body.GetBodyPartChildren(body).ToArray()
            : _body.GetBodyChildren(body).ToArray();
        foreach (var surgery in _surgeries)
        {
            if (GetSingleton(surgery) is not { } surgeryEnt)
                continue;

            foreach (var part in parts)
            {
                var ev = new SurgeryValidEvent(body, part.Id);
                RaiseLocalEvent(surgeryEnt, ref ev);
                if (ev.Cancelled)
                    continue;

                var netPart = GetNetEntity(part.Id);
                if (!surgeries.TryGetValue(netPart, out var partSurgeries))
                    surgeries[netPart] = partSurgeries = new List<EntProtoId>();

                partSurgeries.Add(surgery);
            }
        }

        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries));
    }

    private void OnGetSurgeryVerb(Entity<SurgeryTargetComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            TryComp(ent, out BodyPartComponent? part) && (part.Body != null || part.Parent != null) ||
            (args.User == ent.Owner && !_configuration.GetCVar(CCVars.SurgerySelfEnabled)) ||
            args.Using is not { } tool ||
            !HasComp<SurgeryToolComponent>(tool))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("surgery-verb-open"),
            Icon = new SpriteSpecifier.Rsi(
                new("/Textures/_Onyx/Objects/Specific/Medical/Surgery/scalpel.rsi"),
                "scalpel"),
            Act = () => TryOpenSurgeryUi(ent, user),
        });
    }

    private void TryOpenSurgeryUi(Entity<SurgeryTargetComponent> target, EntityUid user)
    {
        if (target.Owner == user && !_configuration.GetCVar(CCVars.SurgerySelfEnabled))
            return;

        if (!IsReadyForSurgery(target))
        {
            _popup.PopupEntity(
                Loc.GetString("surgery-popup-patient-must-lie"),
                target,
                user,
                PopupType.MediumCaution);
            return;
        }

        _ui.OpenUi(target.Owner, SurgeryUIKey.Key, user);
        RefreshUI(target.Owner);
    }

    private void OnStepBleedComplete(Entity<SurgeryStepBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _wounds.CreateOrMergeWound(args.Part, SurgicalIncision, ent.Comp.Damage);
    }

    private void OnStepClampBleedComplete(Entity<SurgeryClampBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _bleeding.TreatPart(args.Part, BleedingTreatment.Clamped, SurgicalIncision);
    }

    private void OnCloseIncisionComplete(Entity<SurgeryCloseIncisionEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var chance = Math.Clamp(_configuration.GetCVar(CCVars.SurgeryScarChance), 0f, 1f);
        if (!TryComp(args.Part, out WoundableComponent? woundable))
            return;

        foreach (var wound in _wounds.GetWounds((args.Part, woundable)).ToArray())
        {
            if (wound.Comp.Prototype != SurgicalIncision ||
                wound.Comp.State is not WoundState.Open and not WoundState.Stabilized)
                continue;

            _bleeding.SetTreatment(wound.Owner, BleedingTreatment.Cauterized);
            _wounds.CloseWound(wound.Owner);
            if (_random.Prob(chance))
                _scars.CreateScar(wound.Owner);
        }
    }

    private void OnStepEmoteComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }

    protected override void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        base.OnPrototypesReloaded(args);
        if (args.WasModified<EntityPrototype>())
            LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        _surgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (entity.HasComponent<SurgeryComponent>())
                _surgeries.Add(new EntProtoId(entity.ID));
    }
}

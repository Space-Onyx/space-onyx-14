using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Standing;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const string CavityContainer = "surgery_cavity";

    private readonly Dictionary<EntProtoId, EntityUid> _singletons = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
        SubscribeLocalEvent<SurgeryTargetComponent, ComponentStartup>(OnSurgeryTargetStartup);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryCloseIncisionConditionComponent, SurgeryValidEvent>(OnCloseIncisionValid);
        SubscribeLocalEvent<SurgerySpeciesConditionComponent, SurgeryValidEvent>(OnSpeciesConditionValid);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryMissingPartConditionComponent, SurgeryValidEvent>(OnMissingPartConditionValid);
        SubscribeLocalEvent<SurgeryDetachablePartConditionComponent, SurgeryValidEvent>(OnDetachablePartConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        // <Onyx-OrganHealing>
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryValidEvent>(OnOrganHealValid);
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryStepEvent>(OnOrganHeal);
        SubscribeLocalEvent<SurgeryOrganHealEffectComponent, SurgeryStepCompleteCheckEvent>(OnOrganHealCheck);
        // </Onyx-OrganHealing>
        SubscribeLocalEvent<SurgeryCavityConditionComponent, SurgeryValidEvent>(OnCavityConditionValid);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnToolStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteCheckEvent>(OnToolCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnToolCanPerform);
        SubscribeLocalEvent<SurgeryDetachPartEffectComponent, SurgeryStepEvent>(OnDetachPart);
        SubscribeLocalEvent<SurgeryDetachPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnDetachPartCheck);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryStepEvent>(OnAttachPart);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnAttachPartCheck);
        SubscribeLocalEvent<SurgeryAttachPartEffectComponent, SurgeryCanPerformStepEvent>(OnAttachPartCanPerform);
        SubscribeLocalEvent<SurgeryMendAttachedPartEffectComponent, SurgeryStepEvent>(OnMendAttachedPart);
        SubscribeLocalEvent<SurgeryMendAttachedPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnMendAttachedPartCheck);
        SubscribeLocalEvent<SurgerySutureAttachedPartEffectComponent, SurgeryStepEvent>(OnSutureAttachedPart);
        SubscribeLocalEvent<SurgerySutureAttachedPartEffectComponent, SurgeryStepCompleteCheckEvent>(OnSutureAttachedPartCheck);
        SubscribeLocalEvent<SurgeryRemoveOrganEffectComponent, SurgeryStepEvent>(OnRemoveOrgan);
        SubscribeLocalEvent<SurgeryRemoveOrganEffectComponent, SurgeryStepCompleteCheckEvent>(OnRemoveOrganCheck);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryStepEvent>(OnInsertOrgan);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryStepCompleteCheckEvent>(OnInsertOrganCheck);
        SubscribeLocalEvent<SurgeryInsertOrganEffectComponent, SurgeryCanPerformStepEvent>(OnInsertOrganCanPerform);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryStepEvent>(OnInsertCavityItem);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryStepCompleteCheckEvent>(OnInsertCavityItemCheck);
        SubscribeLocalEvent<SurgeryInsertCavityItemEffectComponent, SurgeryCanPerformStepEvent>(OnInsertCavityItemCanPerform);
        SubscribeLocalEvent<SurgeryRemoveCavityItemEffectComponent, SurgeryStepEvent>(OnRemoveCavityItem);
        SubscribeLocalEvent<SurgeryRemoveCavityItemEffectComponent, SurgeryStepCompleteCheckEvent>(OnRemoveCavityItemCheck);
        SubscribeLocalEvent<SurgeryTargetComponent, StandAttemptEvent>(OnTargetStandAttempt);

        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen);
        });
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

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _singletons.Clear();
    }

    protected virtual void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            ClearSingletons();
    }

    protected void ClearSingletons()
    {
        foreach (var singleton in _singletons.Values)
            QueueDel(singleton);

        _singletons.Clear();
    }

    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target != ent.Owner ||
            GetEntity(args.Part) is not { Valid: true } targetPart ||
            !IsSurgeryValid(ent, targetPart, args.Surgery, args.Step, out var surgery, out var part, out var step) ||
            !PreviousStepsComplete(ent, part, surgery, args.Step) ||
            IsStepComplete(ent, part, args.Step) ||
            !CanPerformStep(args.User, ent, part, part.Comp.PartType, step, false))
            return;

        var ev = new SurgeryStepEvent(args.User, ent, part, GetActiveTool(args.User));
        RaiseLocalEvent(step, ref ev);
        // <Onyx-OrganHealing>
        if (_net.IsServer &&
            (HasComp<SurgeryOrganHealEffectComponent>(step) || HasComp<SurgeryClampBleedingEffectComponent>(step)) &&
            !IsStepComplete(ent, part, args.Step) &&
            CanPerformStep(args.User, ent, part, part.Comp.PartType, step, false, out _, out _, out var validTools))
            StartSurgeryDoAfter(ent, part, args.Surgery, args.Step, args.User, step, validTools);
        // </Onyx-OrganHealing>
        RefreshUI(ent);
    }

    private void OnCloseIncisionValid(Entity<SurgeryCloseIncisionConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<IncisionOpenComponent>(args.Part) ||
            !HasComp<SkinRetractedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnSpeciesConditionValid(Entity<SurgerySpeciesConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var species = CompOrNull<HumanoidProfileComponent>(args.Body)?.Species ??
                      CompOrNull<BodyPartComponent>(args.Part)?.Species;
        var matches = species is { } id && ent.Comp.Species.Contains(id);
        if (matches == ent.Comp.Inverse)
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<BodyPartComponent>(args.Part) is not { } part ||
            part.PartType != ent.Comp.Part ||
            ent.Comp.Symmetry is { } symmetry && part.Symmetry != symmetry)
            args.Cancelled = true;
    }

    private void OnMissingPartConditionValid(Entity<SurgeryMissingPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) &&
            !HasComp<BodyPartReattachedComponent>(part) &&
            !HasComp<BodyPartMendedComponent>(part))
            args.Cancelled = true;
    }

    private void OnDetachablePartConditionValid(Entity<SurgeryDetachablePartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_body.TryGetParentBodyPart(args.Part, out _, out _))
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp(args.Part, out BodyPartComponent? part) ||
            ent.Comp.Part is { } requiredPart && part.PartType != requiredPart ||
            _body.TryGetOrganInSlot(args.Part, ent.Comp.Slot.Id, out var organId) == ent.Comp.Inverse)
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Damaged && (!TryComp(organId, out OrganComponent? organ) || organ.Health >= organ.MaxHealth))
            args.Cancelled = true;
    }

    private void OnCavityConditionValid(Entity<SurgeryCavityConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<AbdominalCavityOpenComponent>(args.Part) || CavityOccupied(args.Part) != ent.Comp.Occupied)
            args.Cancelled = true;
    }

    private void OnDetachPart(Entity<SurgeryDetachPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var removedHead = Comp<BodyPartComponent>(args.Part).PartType == BodyPartType.Head;
        if (!_net.IsServer || !_body.TryDetachPart(args.Part))
            return;

        if (removedHead)
            _standing.Down(args.Body, force: true);

        _inventory.RefreshBodySlots(args.Body);
        _hands.TryPickupAnyHand(args.User, args.Part);
    }

    private void OnDetachPartCheck(Entity<SurgeryDetachPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (IsPartOfTarget(args.Body, args.Part))
            args.Cancelled = true;
    }

    private void OnAttachPart(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldPart(args.Tools, ent.Comp.Part, ent.Comp.Symmetry) is not { } part)
            return;

        if (_body.TryAttachPart(args.Part, part))
        {
            RemComp<BodyPartMendedComponent>(part);
            RemComp<BodyPartSuturedComponent>(part);
            EnsureComp<BodyPartReattachedComponent>(part);
            EnsurePartDamageable(part);
            _inventory.RefreshBodySlots(args.Body);
        }
    }

    private void OnTargetStandAttempt(Entity<SurgeryTargetComponent> ent, ref StandAttemptEvent args)
    {
        if (!_body.BodyHasPartType(ent, BodyPartType.Head))
            args.Cancel();
    }

    private void EnsurePartDamageable(EntityUid part)
    {
        EnsureComp<DamageableComponent>(part);
        if (HasComp<InjurableComponent>(part))
            return;

        var injurable = EnsureComp<InjurableComponent>(part);
        injurable.DamageContainer = "Biological";
        Dirty(part, injurable);
    }

    private void OnAttachPartCheck(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartReattachedComponent>(part) &&
            !HasComp<BodyPartMendedComponent>(part) &&
            !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private void OnMendAttachedPart(Entity<SurgeryMendAttachedPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !RemComp<BodyPartReattachedComponent>(part))
            return;

        EnsureComp<BodyPartMendedComponent>(part);
    }

    private void OnMendAttachedPartCheck(Entity<SurgeryMendAttachedPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartMendedComponent>(part) && !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private void OnSutureAttachedPart(Entity<SurgerySutureAttachedPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !RemComp<BodyPartMendedComponent>(part))
            return;

        EnsureComp<BodyPartSuturedComponent>(part);
    }

    private void OnSutureAttachedPartCheck(Entity<SurgerySutureAttachedPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private bool TryGetAttachedPart(EntityUid parent, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid part)
    {
        foreach (var child in _body.GetBodyPartChildren(parent))
        {
            if (child.Component.PartType != type || child.Component.Symmetry != symmetry)
                continue;

            part = child.Id;
            return true;
        }

        part = default;
        return false;
    }

    private void OnAttachPartCanPerform(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (FindHeldPart(args.Tools, ent.Comp.Part, ent.Comp.Symmetry) is not { } part)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-reason-part");
            return;
        }

        if (!_body.AreTransplantsCompatible(args.Part, part))
        {
            args.Invalid = StepInvalidReason.IncompatibleTransplant;
            args.Popup = Loc.GetString("surgery-ui-reason-incompatible-transplant");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(part);
    }

    private void OnRemoveOrgan(Entity<SurgeryRemoveOrganEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !_body.TryRemoveOrgan(args.Part, ent.Comp.Slot.Id, out var organ))
            return;

        _hands.TryPickupAnyHand(args.User, organ);
    }

    private void OnRemoveOrganCheck(Entity<SurgeryRemoveOrganEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_body.TryGetOrganInSlot(args.Part, ent.Comp.Slot.Id, out _))
            args.Cancelled = true;
    }

    private void OnInsertOrgan(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldOrgan(args.Tools, ent.Comp.Slot, ent.Comp.RequireMechanical, ent.Comp.Required) is not { } organ)
            return;

        _body.TryInsertOrgan(args.Part, organ, ent.Comp.Slot.Id);
    }

    private void OnInsertOrganCheck(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_body.TryGetOrganInSlot(args.Part, ent.Comp.Slot.Id, out _))
            args.Cancelled = true;
    }

    private void OnInsertOrganCanPerform(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (FindHeldOrgan(args.Tools, ent.Comp.Slot, ent.Comp.RequireMechanical, ent.Comp.Required) is not { } organ)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-reason-organ");
            return;
        }

        if (!_body.AreTransplantsCompatible(args.Part, organ))
        {
            args.Invalid = StepInvalidReason.IncompatibleTransplant;
            args.Popup = Loc.GetString("surgery-ui-reason-incompatible-transplant");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(organ);
    }

    private EntityUid? FindHeldPart(List<EntityUid> held, BodyPartType type, BodyPartSymmetry symmetry)
    {
        EntityUid? found = null;
        foreach (var item in held)
        {
            if (!TryComp(item, out BodyPartComponent? part) || part.Body != null || part.PartType != type || part.Symmetry != symmetry)
                continue;

            if (found != null)
                return null;
            found = item;
        }

        return found;
    }

    private EntityUid? FindHeldOrgan(List<EntityUid> held, ProtoId<OrganCategoryPrototype> slot, bool requireMechanical,
        ComponentRegistry? required)
    {
        EntityUid? found = null;
        foreach (var item in held)
        {
            if (HasComp<BodyPartComponent>(item) || !TryComp(item, out OrganComponent? organ) || organ.Body != null ||
                organ.Category != slot || requireMechanical && !HasComp<MechanicalOrganComponent>(item) ||
                required != null && required.Values.Any(component => !HasComp(item, component.Component.GetType())))
                continue;

            if (found != null)
                return null;
            found = item;
        }

        return found;
    }

    private void OnInsertCavityItem(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldCavityItem(args.Tools) is not { } item)
            return;

        var container = _containers.EnsureContainer<ContainerSlot>(args.Part, CavityContainer);
        _containers.Insert(item, container);
    }

    private void OnInsertCavityItemCheck(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!CavityOccupied(args.Part))
            args.Cancelled = true;
    }

    private void OnInsertCavityItemCanPerform(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (FindHeldCavityItem(args.Tools) is not { } item)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-reason-cavity-item");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(item);
    }

    private void OnRemoveCavityItem(Entity<SurgeryRemoveCavityItemEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !_containers.TryGetContainer(args.Part, CavityContainer, out var container) ||
            container is not ContainerSlot { ContainedEntity: { } item } || !_containers.Remove(item, container))
            return;

        _hands.TryPickupAnyHand(args.User, item);
    }

    private void OnRemoveCavityItemCheck(Entity<SurgeryRemoveCavityItemEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (CavityOccupied(args.Part))
            args.Cancelled = true;
    }

    private EntityUid? FindHeldCavityItem(List<EntityUid> held)
    {
        EntityUid? found = null;
        var max = _item.GetSizePrototype("Small");
        foreach (var item in held)
        {
            if (!TryComp(item, out ItemComponent? itemComp) || _item.GetSizePrototype(itemComp.Size) > max ||
                HasComp<BodyPartComponent>(item) || HasComp<OrganComponent>(item))
                continue;

            if (found != null)
                return null;
            found = item;
        }
        return found;
    }

    private bool CavityOccupied(EntityUid part)
    {
        return _containers.TryGetContainer(part, CavityContainer, out var container) &&
               container is ContainerSlot { ContainedEntity: not null };
    }

    private void OnToolStep(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.ToolQuality is { } quality && !AnyHaveQuality(args.Tools, quality, out _))
            return;

        if (ent.Comp.Tool != null)
        {
            foreach (var reg in ent.Comp.Tool.Values)
            {
                if (!AnyHaveComp(args.Tools, reg.Component, out var tool))
                    return;

                if (_net.IsServer && TryComp(tool, out SurgeryToolComponent? toolComp) && toolComp.EndSound != null)
                    _audio.PlayPvs(toolComp.EndSound, tool);
            }
        }

        var consumedAmount = Math.Max(1, ent.Comp.ConsumedAmount);
        if (ent.Comp.ConsumedStackType is { } stackType &&
            (!AnyHaveStack(args.Tools, stackType, consumedAmount, out var stack) ||
             _net.IsServer && !_stacks.TryUse(stack, consumedAmount)))
            return;

        if (ent.Comp.ConsumedPrototype is { } prototype &&
            (!TryFindConsumables(args.Tools, prototype, consumedAmount, out var consumables) ||
             _net.IsServer && !ConsumeEntities(consumables)))
            return;

        if (ent.Comp.Add != null)
        {
            foreach (var reg in ent.Comp.Add.Values)
            {
                var type = reg.Component.GetType();
                if (!HasComp(args.Part, type))
                    AddComp(args.Part, _compFactory.GetComponent(type));
            }
        }

        if (ent.Comp.Remove != null)
            foreach (var reg in ent.Comp.Remove.Values)
                RemComp(args.Part, reg.Component.GetType());

        if (ent.Comp.BodyRemove != null)
            foreach (var reg in ent.Comp.BodyRemove.Values)
                RemComp(args.Body, reg.Component.GetType());

        OnToolStepCompleted(ent, ref args);
    }

    protected virtual void OnToolStepCompleted(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
    }

    private void OnToolCheck(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (ent.Comp.Add != null)
            foreach (var reg in ent.Comp.Add.Values)
                if (!HasComp(args.Part, reg.Component.GetType())) { args.Cancelled = true; return; }

        if (ent.Comp.Remove != null)
            foreach (var reg in ent.Comp.Remove.Values)
                if (HasComp(args.Part, reg.Component.GetType())) { args.Cancelled = true; return; }

        if (ent.Comp.BodyRemove != null)
            foreach (var reg in ent.Comp.BodyRemove.Values)
                if (HasComp(args.Body, reg.Component.GetType())) { args.Cancelled = true; return; }
    }

    private void OnToolCanPerform(Entity<SurgeryStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (HasComp<SurgeryOperatingTableConditionComponent>(ent) &&
            (!TryComp(args.Body, out BuckleComponent? buckle) || !HasComp<OperatingTableComponent>(buckle.BuckledTo)))
        {
            args.Invalid = StepInvalidReason.NeedsOperatingTable;
            return;
        }

        RaiseLocalEvent(args.Body, ref args);
        if (args.Invalid != StepInvalidReason.None)
            return;

        args.ValidTools ??= new HashSet<EntityUid>();
        if (ent.Comp.Tool != null)
        {
            foreach (var reg in ent.Comp.Tool.Values)
            {
                if (!AnyHaveComp(args.Tools, reg.Component, out var withComp))
                {
                    args.Invalid = StepInvalidReason.MissingTool;
                    args.Popup = Loc.GetString("surgery-ui-reason-tool");
                    return;
                }

                args.ValidTools.Add(withComp);
            }
        }

        if (ent.Comp.ToolQuality is { } quality)
        {
            if (!AnyHaveQuality(args.Tools, quality, out var tool))
            {
                args.Invalid = StepInvalidReason.MissingTool;
                args.Popup = Loc.GetString("surgery-ui-reason-tool");
                return;
            }

            args.ValidTools.Add(tool);
        }

        if (ent.Comp.ConsumedStackType is { } stackType)
        {
            if (!AnyHaveStack(args.Tools, stackType, Math.Max(1, ent.Comp.ConsumedAmount), out var stack))
            {
                args.Invalid = StepInvalidReason.MissingTool;
                args.Popup = Loc.GetString("surgery-ui-reason-material");
                return;
            }

            args.ValidTools.Add(stack);
        }

        if (ent.Comp.ConsumedPrototype is { } prototype)
        {
            if (!TryFindConsumables(args.Tools, prototype, Math.Max(1, ent.Comp.ConsumedAmount), out var consumables))
            {
                args.Invalid = StepInvalidReason.MissingTool;
                args.Popup = Loc.GetString("surgery-ui-reason-material");
                return;
            }

            args.ValidTools.UnionWith(consumables);
        }
    }

    private void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        var user = args.Actor;
        if (GetEntity(args.Part) is not { Valid: true } targetPart ||
            !IsSurgeryValid(ent, targetPart, args.Surgery, args.Step, out var surgery, out var part, out var step))
            return;

        if (!PreviousStepsComplete(ent, part, surgery, args.Step) || IsStepComplete(ent, part, args.Step))
            return;

        if (!CanPerformStep(user, ent, part, part.Comp.PartType, step, true, out _, out _, out var validTools))
            return;

        if (_net.IsServer && validTools?.Count > 0)
            foreach (var tool in validTools)
                if (TryComp(tool, out SurgeryToolComponent? toolComp) && toolComp.StartSound != null)
                    _audio.PlayPvs(toolComp.StartSound, tool);

        if (TryComp(ent, out TransformComponent? xform))
            _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(ent, xform).Position);

        StartSurgeryDoAfter(ent, part, args.Surgery, args.Step, user, step, validTools);
    }

    // <Onyx-OrganHealing>
    private void StartSurgeryDoAfter(Entity<SurgeryTargetComponent> target, Entity<BodyPartComponent> part,
        EntProtoId surgery, EntProtoId stepId, EntityUid user, EntityUid step, HashSet<EntityUid>? validTools = null)
    {
        var ev = new SurgeryDoAfterEvent(GetNetEntity(part), surgery, stepId);
        var duration = Comp<SurgeryStepComponent>(step).Duration;
        if (validTools != null && TryComp(step, out SurgeryStepComponent? surgeryStep) && surgeryStep.Tool != null)
        {
            foreach (var requirement in surgeryStep.Tool.Values)
            {
                if (!AnyHaveComp(validTools, requirement.Component, out var tool) ||
                    !TryComp(tool, out SurgeryToolComponent? surgeryTool))
                    continue;

                var task = _compFactory.GetComponentName(requirement.Component.GetType());
                if (surgeryTool.SpeedModifiers.TryGetValue(task, out var modifier))
                    duration /= Math.Max(0.01f, modifier);
            }
        }

        if (validTools != null && TryComp(step, out SurgeryStepComponent? toolStep) && toolStep.ToolQuality != null)
        {
            foreach (var tool in validTools)
                if (TryComp(tool, out ToolComponent? toolComp) && _tools.HasQuality(tool, toolStep.ToolQuality, toolComp))
                {
                    duration /= Math.Max(0.01f, toolComp.SpeedModifier);
                    break;
                }
        }

        if (user == target.Owner)
            duration *= Math.Max(0.01f, _configuration.GetCVar(CCVars.SurgerySelfMultiplier));

        if (TryComp<SurgerySpeedModifierComponent>(user, out var speedModifier))
            duration /= Math.Max(0.01f, speedModifier.SpeedModifier);

        var doAfter = new DoAfterArgs(EntityManager, user, duration, ev, target, target)
        {
            NeedHand = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }
    // </Onyx-OrganHealing>

    // <Onyx-OrganHealing>
    private void OnOrganHealValid(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Amount <= FixedPoint2.Zero || !TryFindOrgan(args.Part, ent.Comp.Slot, out var organ) ||
            organ.Comp.Health >= organ.Comp.MaxHealth)
            args.Cancelled = true;
    }

    private void OnOrganHeal(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryFindOrgan(args.Part, ent.Comp.Slot, out var organ))
            return;

        organ.Comp.Health = FixedPoint2.Min(organ.Comp.MaxHealth, organ.Comp.Health + ent.Comp.Amount);
        Dirty(organ.Owner, organ.Comp);
    }

    private void OnOrganHealCheck(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryFindOrgan(args.Part, ent.Comp.Slot, out var organ) || organ.Comp.Health < organ.Comp.MaxHealth)
            args.Cancelled = true;
    }

    private bool TryFindOrgan(EntityUid part, ProtoId<OrganCategoryPrototype> slot, out Entity<OrganComponent> organ)
    {
        organ = default;
        if (!_body.TryGetOrganInSlot(part, slot, out var organId) || !TryComp(organId, out OrganComponent? component))
            return false;

        organ = (organId, component);
        return true;
    }
    // </Onyx-OrganHealing>

    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId, out Entity<SurgeryComponent> surgeryEnt, out Entity<BodyPartComponent> part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body) || !IsReadyForSurgery(body) ||
            !TryComp(targetPart, out BodyPartComponent? partComp) || !IsPartOfTarget(body, targetPart) ||
            GetSingleton(surgery) is not { } surgeryEntId || !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !surgeryComp.Steps.Contains(stepId) || GetSingleton(stepId) is not { } stepEnt)
            return false;

        var ev = new SurgeryValidEvent(body, targetPart);
        RaiseLocalEvent(stepEnt, ref ev);
        RaiseLocalEvent(surgeryEntId, ref ev);
        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = (targetPart, partComp);
        step = stepEnt;
        return true;
    }

    public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
    {
        if (!_prototypes.HasIndex(surgeryOrStep))
            return null;

        if (!_singletons.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
            _singletons[surgeryOrStep] = ent;
        }

        return ent;
    }

    private List<EntityUid> GetActiveTool(EntityUid surgeon)
    {
        var tools = new List<EntityUid>(1);
        if (_hands.GetActiveItem(surgeon) is { } item)
            tools.Add(item);

        return tools;
    }

    public bool IsLyingDown(EntityUid entity)
    {
        if (_standing.IsDown(entity))
            return true;

        return TryComp(entity, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out StrapComponent? strap) &&
               strap.Position == StrapPosition.Down;
    }

    public bool IsReadyForSurgery(EntityUid entity)
    {
        return TryComp(entity, out BodyPartComponent? part)
            ? part.Body == null && part.Parent == null
            : IsLyingDown(entity);
    }

    public bool IsPartOfTarget(EntityUid target, EntityUid part)
    {
        return TryComp(target, out BodyPartComponent? targetPart)
            ? targetPart.Body == null && targetPart.Parent == null && _body.GetBodyPartChildren(target).Any(child => child.Id == part)
            : _body.BodyHasChild(target, part);
    }

    private bool AnyHaveComp(IEnumerable<EntityUid> entities, IComponent component, out EntityUid found)
    {
        var type = component.GetType();
        foreach (var entity in entities)
        {
            if (!HasComp(entity, type))
                continue;

            found = entity;
            return true;
        }

        found = default;
        return false;
    }

    private bool AnyHaveQuality(IEnumerable<EntityUid> entities, ProtoId<ToolQualityPrototype> quality, out EntityUid found)
    {
        foreach (var entity in entities)
        {
            if (!_tools.HasQuality(entity, quality))
                continue;

            found = entity;
            return true;
        }

        found = default;
        return false;
    }

    private bool AnyHaveStack(IEnumerable<EntityUid> entities, ProtoId<StackPrototype> stackType, int amount,
        out EntityUid found)
    {
        foreach (var entity in entities)
        {
            if (!TryComp(entity, out StackComponent? stack) || stack.StackTypeId != stackType || stack.Count < amount)
                continue;

            found = entity;
            return true;
        }

        found = default;
        return false;
    }

    private bool TryFindConsumables(IEnumerable<EntityUid> entities, EntProtoId prototype, int amount,
        out List<EntityUid> found)
    {
        found = new List<EntityUid>(amount);
        foreach (var entity in entities)
        {
            if (MetaData(entity).EntityPrototype is not { } entityPrototype || entityPrototype.ID != prototype.Id)
                continue;

            found.Add(entity);
            if (found.Count == amount)
                return true;
        }

        found.Clear();
        return false;
    }

    private bool ConsumeEntities(List<EntityUid> entities)
    {
        foreach (var entity in entities)
        {
            if (TerminatingOrDeleted(entity))
                return false;

            QueueDel(entity);
        }

        return true;
    }

    public (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, Entity<SurgeryComponent?> surgery, List<EntityUid> requirements)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop");

        requirements.Add(surgery);
        if (surgery.Comp.Requirement is { } requirementId && GetSingleton(requirementId) is { } requirement &&
            TryComp(requirement, out SurgeryComponent? requirementComp) &&
            !IsSurgeryComplete(body, part, (requirement, requirementComp)) &&
            GetNextStep(body, part, (requirement, requirementComp), requirements) is { } requiredNext)
            return requiredNext;

        for (var i = 0; i < surgery.Comp.Steps.Count; i++)
            if (!IsStepComplete(body, part, surgery.Comp.Steps[i]))
                return ((surgery, surgery.Comp), i);

        return null;
    }

    public bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step)
    {
        if (surgery.Comp.Requirement is { } requirement &&
            (GetSingleton(requirement) is not { } requiredEnt ||
             !TryComp(requiredEnt, out SurgeryComponent? requiredComp) ||
             !IsSurgeryComplete(body, part, (requiredEnt, requiredComp))))
            return false;

        foreach (var surgeryStep in surgery.Comp.Steps)
        {
            if (surgeryStep == step)
                break;
            if (!IsStepComplete(body, part, surgeryStep))
                return false;
        }

        return true;
    }

    private bool IsSurgeryComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery)
    {
        return surgery.Comp.Steps.All(step => IsStepComplete(body, part, step));
    }

    public bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId stepId)
    {
        if (GetSingleton(stepId) is not { } step)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part);
        RaiseLocalEvent(step, ref ev);
        return !ev.Cancelled;
    }

    public bool CanPerformStep(EntityUid user, EntityUid body, EntityUid targetPart, BodyPartType part, EntityUid step, bool doPopup, out string? popup, out StepInvalidReason reason, out HashSet<EntityUid>? validTools)
    {
        if (!_interaction.InRangeUnobstructed(user, body, popup: doPopup))
        {
            popup = "You are too far away from the patient.";
            reason = StepInvalidReason.OutOfRange;
            validTools = null;
            return false;
        }

        var slot = part switch
        {
            BodyPartType.Head => SlotFlags.HEAD,
            BodyPartType.Torso or BodyPartType.Chest or BodyPartType.Groin or BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };

        if (slot != SlotFlags.NONE && TryComp(body, out InventoryComponent? inventory))
        {
            var equipped = new InventorySystem.InventorySlotEnumerator(inventory, slot);
            if (equipped.NextItem(out _))
            {
                popup = "Remove clothing covering the surgical site.";
                validTools = null;
                if (doPopup)
                    _popup.PopupEntity(popup, user, PopupType.SmallCaution);
                reason = StepInvalidReason.Clothing;
                return false;
            }
        }

        var check = new SurgeryCanPerformStepEvent(user, body, targetPart, GetActiveTool(user), slot);
        RaiseLocalEvent(step, ref check);
        popup = check.Popup;
        validTools = check.ValidTools;

        if (check.Invalid == StepInvalidReason.None)
        {
            reason = default;
            return true;
        }

        if (doPopup && check.Popup != null)
            _popup.PopupEntity(check.Popup, user, PopupType.SmallCaution);

        reason = check.Invalid;
        return false;
    }

    public bool CanPerformStep(EntityUid user, EntityUid body, EntityUid targetPart, BodyPartType part, EntityUid step, bool doPopup)
        => CanPerformStep(user, body, targetPart, part, step, doPopup, out _, out _, out _);

    protected virtual void RefreshUI(EntityUid body) { }
}

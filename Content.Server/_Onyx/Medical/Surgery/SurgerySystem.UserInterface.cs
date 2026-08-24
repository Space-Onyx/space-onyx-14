using System.Linq;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgerySystem
{
    private HashSet<EntityUid> _pendingUiRefresh = new();
    private HashSet<EntityUid> _processingUiRefresh = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingUiRefresh.Count == 0)
            return;

        (_pendingUiRefresh, _processingUiRefresh) = (_processingUiRefresh, _pendingUiRefresh);
        foreach (var body in _processingUiRefresh)
        {
            if (_ui.IsUiOpen(body, SurgeryUIKey.Key))
                RefreshUI(body);
        }
        _processingUiRefresh.Clear();
    }

    private void OnPatientStateChanged<T>(Entity<WoundComponent> wound, ref T args) where T : notnull
    {
        var target = CompOrNull<BodyPartComponent>(wound.Comp.HoldingPart)?.Body ?? wound.Comp.HoldingPart;
        QueueUiRefresh(target);
    }

    private void OnBodyOrganSlotChanged(Entity<BodyComponent> body, ref BodyOrganSlotChangedEvent args)
    {
        QueueUiRefresh(body.Owner);
    }

    private void QueueUiRefresh(EntityUid body)
    {
        if (_ui.IsUiOpen(body, SurgeryUIKey.Key))
            _pendingUiRefresh.Add(body);
    }

    private void OnUiOpened(Entity<SurgeryTargetComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshUI(ent);
    }

    private void OnStepsStateRequest(Entity<SurgeryTargetComponent> ent, ref SurgeryStepsStateRequest args)
    {
        var part = GetEntity(args.Part);
        if (!TryComp(part, out BodyPartComponent? partComp) ||
            GetSurgeryEntity(args.Surgery) is not { } surgery ||
            !TryComp(surgery, out SurgeryComponent? surgeryComp))
        {
            SendStepsState(ent, args, [], [], -1, false, null, StepInvalidReason.None, SurgerySelectionState.Invalid);
            return;
        }

        var tools = GetActiveTool(args.Actor);
        var steps = GetSurgerySteps(ent, part, (surgery, surgeryComp), tools).ToList();
        var completed = new List<bool>(steps.Count);
        foreach (var step in steps)
            completed.Add(IsSurgeryItemComplete(ent, part, step, tools));

        if (!IsPartOfTarget(ent, part) || !IsReadyForSurgery(ent))
        {
            SendStepsState(ent, args, steps, completed, -1, false, null, StepInvalidReason.None,
                SurgerySelectionState.Invalid);
            return;
        }

        var nextStep = GetNextStep(ent, part, (surgery, surgeryComp), tools) ?? -1;
        if (completed.All(static step => step))
        {
            SendStepsState(ent, args, steps, completed, -1, false, null, StepInvalidReason.None,
                SurgerySelectionState.Completed);
            return;
        }

        var valid = new SurgeryValidEvent(ent, part);
        if (nextStep >= 0)
        {
            if (GetSurgeryStepEntity(steps[nextStep]) is { } selectedStep)
                RaiseLocalEvent(selectedStep, ref valid);
            else if (GetSurgeryEntity(steps[nextStep]) is { } nestedSurgery)
                RaiseLocalEvent(nestedSurgery, ref valid);
        }
        RaiseLocalEvent(surgery, ref valid);
        if (valid.Cancelled)
        {
            SendStepsState(ent, args, steps, completed, -1, false, null, StepInvalidReason.None,
                SurgerySelectionState.Invalid);
            return;
        }

        var available = false;
        string? popup = null;
        var reason = StepInvalidReason.None;
        if (!ActiveSurgerySites.ContainsKey((ent.Owner, part)) && nextStep >= 0)
        {
            if (GetSurgeryStepEntity(steps[nextStep]) is { } stepEnt)
                available = CanPerformStep(args.Actor, ent, part, partComp.PartType, stepEnt, false,
                    out popup, out reason, out _);
            else
                available = GetSurgeryEntity(steps[nextStep]) != null;
        }

        SendStepsState(ent, args, steps, completed, nextStep, available, popup, reason, SurgerySelectionState.Active);
    }

    private void SendStepsState(Entity<SurgeryTargetComponent> ent, SurgeryStepsStateRequest args,
        List<EntProtoId> steps, List<bool> completed, int nextStep, bool available, string? popup, StepInvalidReason reason,
        SurgerySelectionState selectionState)
    {
        _ui.ServerSendUiMessage(ent.Owner, SurgeryUIKey.Key,
            new SurgeryStepsStateResponse(args.Part, args.Surgery, steps, completed, nextStep, available, popup, reason,
                args.RequestId, selectionState), args.Actor);
    }

    protected override void RefreshUI(EntityUid body)
    {
        if (!HasComp<SurgeryTargetComponent>(body))
            return;

        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        var completed = new Dictionary<NetEntity, HashSet<EntProtoId>>();
        var parts = TryComp(body, out BodyPartComponent? rootPart)
            ? _body.GetBodyPartChildren(body).ToArray()
            : _body.GetBodyChildren(body).ToArray();
        foreach (var surgery in SurgeryPrototypes)
        {
            if (GetSurgeryEntity(surgery) is not { } surgeryEnt)
                continue;

            foreach (var part in parts)
            {
                var netPart = GetNetEntity(part.Id);
                if (TryComp(surgeryEnt, out SurgeryComponent? surgeryComp) &&
                    GetSurgerySteps(body, part.Id, (surgeryEnt, surgeryComp), [])
                        .All(step => IsSurgeryItemComplete(body, part.Id, step, [])))
                {
                    if (!completed.TryGetValue(netPart, out var partCompleted))
                        completed[netPart] = partCompleted = new HashSet<EntProtoId>();

                    partCompleted.Add(surgery);
                }

                var ev = new SurgeryValidEvent(body, part.Id);
                RaiseLocalEvent(surgeryEnt, ref ev);
                if (ev.Cancelled)
                    continue;

                if (!surgeries.TryGetValue(netPart, out var partSurgeries))
                    surgeries[netPart] = partSurgeries = new List<EntProtoId>();

                partSurgeries.Add(surgery);
            }
        }

        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries, completed));
    }

    private void OnGetSurgeryVerb(Entity<SurgeryTargetComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            TryComp(ent, out BodyPartComponent? part) && (part.Body != null || part.Parent != null) ||
            (args.User == ent.Owner && !_configuration.GetCVar(CCVars.SurgerySelfEnabled)) ||
            args.Using is not { } tool ||
            !HasComp<SurgeryToolComponent>(tool) &&
            !(IsIpc(ent) && HasComp<ToolComponent>(tool)))
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

    private bool IsIpc(EntityUid target)
    {
        var species = CompOrNull<HumanoidProfileComponent>(target)?.Species ??
                      CompOrNull<BodyPartComponent>(target)?.Species;
        return species == "Ipc";
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
    }
}

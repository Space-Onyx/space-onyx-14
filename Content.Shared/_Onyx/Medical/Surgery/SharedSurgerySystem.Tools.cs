using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
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

        if (_net.IsServer)
        {
            UpdateMarkers(args.Part, ent.Comp.AddMarkers, ent.Comp.RemoveMarkers);
            if (ent.Comp.ParentRemoveMarkers.Count > 0 &&
                (ent.Comp.ParentRemoveMarkersPart == null ||
                 CompOrNull<BodyPartComponent>(args.Part)?.PartType == ent.Comp.ParentRemoveMarkersPart) &&
                _body.TryGetParentBodyPart(args.Part, out var parent, out _) && parent is { } parentPart)
                UpdateMarkers(parentPart, [], ent.Comp.ParentRemoveMarkers);
        }

        OnToolStepCompleted(ent, ref args);
    }

    private void OnPainInflicterStep(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepEvent args)
    {
        if (HasComp<MechanicalSurgeryStepComponent>(ent))
            return;

        var amount = ent.Comp.Amount;
        if (HasComp<SleepingComponent>(args.Body))
            amount *= ent.Comp.SleepModifier;

        _pain.ChangePain((args.Part, null), amount);
    }

    protected virtual void OnToolStepCompleted(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
    }

    private void OnToolCheck(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var markers = CompOrNull<SurgeryMarkerComponent>(args.Part)?.Markers;
        if (ent.Comp.AddMarkers.Any(marker => markers == null || !markers.Contains(marker)) ||
            markers != null && ent.Comp.RemoveMarkers.Any(markers.Contains))
            args.Cancelled = true;
    }

    private void UpdateMarkers(EntityUid part, HashSet<string> add, HashSet<string> remove)
    {
        if (add.Count == 0 && remove.Count == 0)
            return;

        var markers = EnsureComp<SurgeryMarkerComponent>(part);
        var changed = false;
        foreach (var marker in remove)
            changed |= markers.Markers.Remove(marker);
        foreach (var marker in add)
            changed |= markers.Markers.Add(marker);

        if (changed)
            Dirty(part, markers);
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
                    SetMissingTool(ref args, "surgery-ui-reason-tool");
                    return;
                }

                args.ValidTools.Add(withComp);
            }
        }

        if (ent.Comp.ToolQuality is { } quality)
        {
            if (!AnyHaveQuality(args.Tools, quality, out var tool))
            {
                SetMissingTool(ref args, "surgery-ui-reason-tool");
                return;
            }

            args.ValidTools.Add(tool);
        }

        if (ent.Comp.ConsumedStackType is { } stackType)
        {
            if (!AnyHaveStack(args.Tools, stackType, Math.Max(1, ent.Comp.ConsumedAmount), out var stack))
            {
                SetMissingMaterial(ref args);
                return;
            }

            args.ValidTools.Add(stack);
        }

        if (ent.Comp.ConsumedPrototype is { } prototype)
        {
            if (!TryFindConsumables(args.Tools, prototype, Math.Max(1, ent.Comp.ConsumedAmount), out var consumables))
            {
                SetMissingMaterial(ref args);
                return;
            }

            args.ValidTools.UnionWith(consumables);
        }
    }

    private void SetMissingTool(ref SurgeryCanPerformStepEvent args, string localizationKey)
    {
        args.Invalid = StepInvalidReason.MissingTool;
        args.Popup = Loc.GetString(localizationKey);
    }

    private void SetMissingMaterial(ref SurgeryCanPerformStepEvent args)
    {
        args.Invalid = StepInvalidReason.MissingMaterial;
        args.Popup = Loc.GetString("surgery-ui-reason-material");
    }

    protected float GetSurgerySuccessRate(EntityUid step, IEnumerable<EntityUid>? validTools)
    {
        if (validTools == null || !TryComp(step, out SurgeryStepComponent? surgeryStep) || surgeryStep.Tool == null)
            return 1f;

        var successRate = 1f;
        foreach (var requirement in surgeryStep.Tool.Values)
        {
            if (!AnyHaveComp(validTools, requirement.Component, out var tool) ||
                !TryComp(tool, out SurgeryToolComponent? surgeryTool))
                continue;

            var task = _compFactory.GetComponentName(requirement.Component.GetType());
            successRate = Math.Min(successRate, Math.Clamp(surgeryTool.SuccessModifiers.GetValueOrDefault(task, 1f), 0f, 1f));
        }

        return successRate;
    }

    protected List<EntityUid> GetActiveTool(EntityUid surgeon)
    {
        var tools = new List<EntityUid>(1);
        if (_hands.GetActiveItem(surgeon) is { } item)
            tools.Add(item);

        return tools;
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
}

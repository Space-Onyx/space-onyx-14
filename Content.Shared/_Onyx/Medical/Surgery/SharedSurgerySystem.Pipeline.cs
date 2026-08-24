using Content.Shared.Body.Part;
using Content.Shared.CCVar;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Tools.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_net.IsServer || args.Handled || args.Target != ent.Owner)
            return;

        args.Handled = true;
        if (GetEntity(args.Part) is not { Valid: true } targetPart)
        {
            RemoveSurgerySite(ent.Owner, args.Token);
            RefreshUI(ent);
            return;
        }

        var site = (Body: ent.Owner, Part: targetPart);
        if (!ActiveSurgerySites.TryGetValue(site, out var active) || active.Token != args.Token)
            return;

        ActiveSurgerySites.Remove(site);
        if (args.Cancelled)
        {
            RefreshUI(ent);
            return;
        }

        var tools = GetActiveTool(args.User);
        if (!TryValidateSurgeryStep(args.User, ent, targetPart, args.Surgery, args.Step, tools, false,
                out var part, out var step, out _))
        {
            RefreshUI(ent);
            return;
        }

        var ev = new SurgeryStepEvent(args.User, ent, part, tools);
        RaiseLocalEvent(step, ref ev);
        if (HasComp<RepeatSurgeryStepComponent>(step) &&
            !IsStepComplete(ent, part, args.Step) &&
            CanPerformStep(args.User, ent, part, part.Comp.PartType, step, false, out _, out _, out var validTools))
        {
            var nextToken = ReserveSurgerySite(site, args.User);
            _pendingSurgeryRepeats.Add(new(ent, part, args.User, args.Surgery, args.Step, nextToken));
        }
        RefreshUI(ent);
    }

    protected void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        if (!_net.IsServer)
            return;

        var user = args.Actor;
        var targetPart = GetEntity(args.Part);
        if (!targetPart.Valid)
            return;

        var tools = GetActiveTool(user);
        var site = (Body: ent.Owner, Part: targetPart);
        if (ActiveSurgerySites.ContainsKey(site))
            return;

        var token = ReserveSurgerySite(site, user);
        if (!TryValidateSurgeryStep(user, ent, targetPart, args.Surgery, args.Step, tools, true,
                out var part, out var step, out var validTools))
        {
            RemoveSurgerySite(site, token);
            return;
        }

        if (_net.IsServer && validTools?.Count > 0)
            foreach (var tool in validTools)
                if (TryComp(tool, out SurgeryToolComponent? toolComp) && toolComp.StartSound != null)
                    _audio.PlayPvs(toolComp.StartSound, tool);

        if (TryComp(ent, out TransformComponent? xform))
            _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(ent, xform).Position);

        if (!StartSurgeryDoAfter(ent, part, args.Surgery, args.Step, user, step, token, validTools))
            RemoveSurgerySite(site, token);
    }

    private bool StartSurgeryDoAfter(Entity<SurgeryTargetComponent> target, Entity<BodyPartComponent> part,
        EntProtoId surgery, EntProtoId stepId, EntityUid user, EntityUid step, uint token,
        HashSet<EntityUid>? validTools = null)
    {
        var ev = new SurgeryDoAfterEvent(GetNetEntity(part), surgery, stepId, token);
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
        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        var userName = Identity.Entity(user, EntityManager);
        var targetName = Identity.Entity(target, EntityManager);
        var procedureKey = $"surgery-popup-procedure-{surgery}-step-{stepId}";
        if (!Loc.TryGetString(procedureKey, out var popup, ("user", userName), ("target", targetName), ("part", part)))
            Loc.TryGetString($"surgery-popup-step-{stepId}", out popup, ("user", userName), ("target", targetName), ("part", part));

        if (popup != null)
        {
            _popup.PopupEntity(popup, user, user);
            _popup.PopupEntity(popup, user, Filter.PvsExcept(user), true);
        }

        return true;
    }

    private uint ReserveSurgerySite((EntityUid Body, EntityUid Part) site, EntityUid user)
    {
        var token = ++_nextSurgeryToken;
        ActiveSurgerySites.Add(site, new(token, user));
        return token;
    }

    private void RemoveSurgerySite((EntityUid Body, EntityUid Part) site, uint token)
    {
        if (ActiveSurgerySites.TryGetValue(site, out var active) && active.Token == token)
            ActiveSurgerySites.Remove(site);
    }

    private void RemoveSurgerySite(EntityUid body, uint token)
    {
        foreach (var (site, active) in ActiveSurgerySites)
        {
            if (site.Body != body || active.Token != token)
                continue;

            ActiveSurgerySites.Remove(site);
            return;
        }
    }

    private void ProcessPendingSurgeryRepeats()
    {
        if (_pendingSurgeryRepeats.Count == 0)
            return;

        (_pendingSurgeryRepeats, _processingSurgeryRepeats) = (_processingSurgeryRepeats, _pendingSurgeryRepeats);
        foreach (var pending in _processingSurgeryRepeats)
        {
            var site = (pending.Body, pending.Part);
            if (!ActiveSurgerySites.TryGetValue(site, out var active) ||
                active.Token != pending.Token ||
                active.User != pending.User)
                continue;

            var tools = GetActiveTool(pending.User);
            if (!TryComp(pending.Body, out SurgeryTargetComponent? targetComp) ||
                !TryValidateSurgeryStep(pending.User, (pending.Body, targetComp), pending.Part, pending.Surgery,
                    pending.Step, tools, false, out var part, out var step, out var validTools) ||
                !StartSurgeryDoAfter((pending.Body, targetComp), part, pending.Surgery, pending.Step,
                    pending.User, step, pending.Token, validTools))
                RemoveSurgerySite(site, pending.Token);
        }

        _processingSurgeryRepeats.Clear();
    }
}

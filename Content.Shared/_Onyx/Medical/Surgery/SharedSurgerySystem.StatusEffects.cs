using Content.Shared.CombatMode.Pacification;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnPacificationConditionValid(Entity<SurgeryPacificationConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_net.IsClient)
            return;

        var surgicallyPacified = HasComp<SurgicallyPacifiedComponent>(args.Body);
        if (ent.Comp.Pacified ? !surgicallyPacified : surgicallyPacified || HasComp<PacifiedComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnPacificationEffect(Entity<SurgeryPacificationEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        if (ent.Comp.Remove)
        {
            if (!RemComp<SurgicallyPacifiedComponent>(args.Body))
                return;

            RemComp<PacifiedComponent>(args.Body);
            return;
        }

        if (HasComp<PacifiedComponent>(args.Body))
            return;

        EnsureComp<PacifiedComponent>(args.Body);
        EnsureComp<SurgicallyPacifiedComponent>(args.Body);
    }

    private void OnPacificationEffectCheck(Entity<SurgeryPacificationEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var surgicallyPacified = HasComp<SurgicallyPacifiedComponent>(args.Body);
        if (ent.Comp.Remove ? surgicallyPacified : !surgicallyPacified || !HasComp<PacifiedComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnMutingConditionValid(Entity<SurgeryMutingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_net.IsClient)
            return;

        if (_statusEffects.HasStatusEffect(args.Body, SurgicallyMutedEffect) != ent.Comp.Muted)
            args.Cancelled = true;
    }

    private void OnMutingEffect(Entity<SurgeryMutingEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Remove)
            _statusEffects.TryRemoveStatusEffect(args.Body, SurgicallyMutedEffect);
        else
            _statusEffects.TrySetStatusEffectDuration(args.Body, SurgicallyMutedEffect);
    }

    private void OnMutingEffectCheck(Entity<SurgeryMutingEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_statusEffects.HasStatusEffect(args.Body, SurgicallyMutedEffect) == ent.Comp.Remove)
            args.Cancelled = true;
    }
}

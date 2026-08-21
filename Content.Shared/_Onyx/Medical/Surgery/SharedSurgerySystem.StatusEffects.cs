namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
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

using Content.Shared._Onyx.Surgery.Organs;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnMutingConditionValid(Entity<SurgeryMutingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryGetTongue(args.Part, out var tongue) || tongue.Comp.VocalCordsCut != ent.Comp.Muted)
            args.Cancelled = true;
    }

    private void OnMutingEffect(Entity<SurgeryMutingEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryGetTongue(args.Part, out var tongue))
            return;

        tongue.Comp.VocalCordsCut = !ent.Comp.Remove;
        Dirty(tongue);
        if (ent.Comp.Remove)
            _statusEffects.TryRemoveStatusEffect(args.Body, SurgicallyMutedEffect);
        else
            _statusEffects.TrySetStatusEffectDuration(args.Body, SurgicallyMutedEffect);
    }

    private void OnMutingEffectCheck(Entity<SurgeryMutingEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetTongue(args.Part, out var tongue) || tongue.Comp.VocalCordsCut == ent.Comp.Remove)
            args.Cancelled = true;
    }

    private bool TryGetTongue(EntityUid part, out Entity<TongueComponent> tongue)
    {
        tongue = default;
        if (!_body.TryGetOrganInSlot(part, "Tongue", out var entity) ||
            !TryComp(entity, out TongueComponent? component))
            return false;

        tongue = (entity, component);
        return true;
    }
}

using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnComponentEffect(Entity<SurgeryComponentEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        var target = ent.Comp.Target == SurgeryEntityTarget.Body ? args.Body : args.Part;
        EntityManager.RemoveComponents(target, ent.Comp.Remove);
        EntityManager.AddComponents(target, ent.Comp.Add, removeExisting: false);
    }

    private void OnComponentEffectCheck(Entity<SurgeryComponentEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        var target = ent.Comp.Target == SurgeryEntityTarget.Body ? args.Body : args.Part;
        if (ent.Comp.Add.Values.Any(component => !HasComp(target, component.Component.GetType())) ||
            ent.Comp.Remove.Values.Any(component => HasComp(target, component.Component.GetType())))
            args.Cancelled = true;
    }
}

using System.Linq;
using Robust.Shared.Prototypes;

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
        if (!ComponentsMatch(target, ent.Comp.Add, ent.Comp.Remove))
            args.Cancelled = true;
    }

    private bool ComponentsMatch(EntityUid target, ComponentRegistry required, ComponentRegistry forbidden)
    {
        return required.Values.All(component => HasComp(target, component.Component.GetType())) &&
               forbidden.Values.All(component => !HasComp(target, component.Component.GetType()));
    }
}

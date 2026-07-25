using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared.Item.ItemToggle;

public sealed partial class ComponentTogglerSystem
{
    public void ToggleComponent(EntityUid uid, bool activate)
    {
        if (!TryComp<ComponentTogglerComponent>(uid, out var component))
            return;

        if (activate)
        {
            var target = component.Parent ? Transform(uid).ParentUid : uid;
            if (TerminatingOrDeleted(target))
                return;
            component.Target = target;
            EntityManager.AddComponents(target, component.Components);
        }
        else if (component.Target is { } target && !TerminatingOrDeleted(target))
            EntityManager.RemoveComponents(target, component.RemoveComponents ?? component.Components);
    }
}

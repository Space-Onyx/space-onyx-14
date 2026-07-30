using Content.Shared.Mind.Components;

namespace Content.Shared.Mind;

public abstract partial class SharedMindSystem
{
    public void ClearObjectives(EntityUid mind, MindComponent? component = null)
    {
        if (!Resolve(mind, ref component))
            return;

        foreach (var objective in component.Objectives)
            QueueDel(objective);

        component.Objectives.Clear();
        Dirty(mind, component);
    }
}

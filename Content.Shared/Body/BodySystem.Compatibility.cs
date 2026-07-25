using JetBrains.Annotations;
using Content.Shared.Body.Systems;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    /// <summary>
    /// Returns a list of organs with a given component in the body.
    /// This is only provided to ease migration from the older BodySystem and should not be used in new code.
    /// </summary>
    /// <param name="ent">The body to query.</param>
    /// <param name="organs">The set of organs with the given component.</param>
    /// <typeparam name="TComp">The component to test for.</typeparam>
    /// <returns>Whether any organs were returned.</returns>
    [Obsolete("Use an event-relay based approach instead")]
    [PublicAPI]
    public bool TryGetOrgansWithComponent<TComp>(Entity<BodyComponent?> ent, out List<Entity<TComp>> organs) where TComp : Component
    {
        organs = new();
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            return false;

        // <Onyx-Surgery-edited>
        var seen = new HashSet<EntityUid>();
        if (ent.Comp.Organs != null)
        {
            foreach (var organ in ent.Comp.Organs.ContainedEntities)
            {
                if (TryComp<TComp>(organ, out var comp) && seen.Add(organ))
                    organs.Add((organ, comp));
            }
        }

        var graph = EntityManager.System<SharedBodySystem>();
        foreach (var part in graph.GetBodyChildren(ent))
        {
            if (TryComp<TComp>(part.Id, out var comp) && seen.Add(part.Id))
                organs.Add((part.Id, comp));
        }

        foreach (var organ in graph.GetBodyOrgans(ent))
        {
            if (TryComp<TComp>(organ.Id, out var comp) && seen.Add(organ.Id))
                organs.Add((organ.Id, comp));
        }
        // </Onyx-Surgery-edited>

        return organs.Count != 0;
    }
}

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Cybernetics.Personalization;

public static class RoundstartCyberneticsResolver
{
    public static List<EntProtoId> GetSelectable(IPrototypeManager prototypes, IComponentFactory factory)
    {
        return prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract && TryGetData(proto, factory, out var data) && data.Selectable)
            .OrderBy(proto => proto.Name)
            .ThenBy(proto => proto.ID)
            .Select(proto => new EntProtoId(proto.ID))
            .ToList();
    }

    public static List<EntProtoId> Normalize(
        IEnumerable<EntProtoId> selections,
        int capacity,
        IPrototypeManager prototypes,
        IComponentFactory factory)
    {
        var result = new List<EntProtoId>();
        var seen = new HashSet<EntProtoId>();

        foreach (var selection in selections)
        {
            if (!seen.Add(selection) || !TryResolve([selection], prototypes, factory, out _, out _))
                continue;

            if (!TryResolve(result.Append(selection), prototypes, factory, out _, out var totalCost) || totalCost > capacity)
                continue;

            result.Add(selection);
        }

        return result;
    }

    public static bool TryResolve(
        IEnumerable<EntProtoId> selections,
        IPrototypeManager prototypes,
        IComponentFactory factory,
        out List<EntProtoId> resolved,
        out int cost)
    {
        var resolvedParts = new List<EntProtoId>();
        var totalCost = 0;
        var visiting = new HashSet<EntProtoId>();
        var visited = new HashSet<EntProtoId>();

        foreach (var selection in selections)
        {
            if (!Resolve(selection, true))
            {
                resolved = [];
                cost = 0;
                return false;
            }
        }

        resolved = resolvedParts;
        cost = totalCost;
        return true;

        bool Resolve(EntProtoId id, bool requireSelectable)
        {
            if (visited.Contains(id))
                return true;
            if (!visiting.Add(id) || !prototypes.TryIndex(id, out EntityPrototype? proto) || proto.Abstract ||
                !TryGetData(proto, factory, out var data) || requireSelectable && !data.Selectable)
                return false;

            foreach (var dependency in data.Dependencies)
            {
                if (!Resolve(dependency, false))
                    return false;
            }

            visiting.Remove(id);
            visited.Add(id);
            resolvedParts.Add(id);
            totalCost += data.Cost;
            return true;
        }
    }

    public static bool TryGetData(EntityPrototype prototype, IComponentFactory factory, out RoundstartCyberneticsComponent data)
    {
        if (prototype.TryGetComponent(out RoundstartCyberneticsComponent? component, factory))
        {
            data = component;
            return true;
        }

        data = default!;
        return false;
    }
}

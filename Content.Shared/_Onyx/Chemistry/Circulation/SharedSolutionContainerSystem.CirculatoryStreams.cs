using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;

namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedSolutionContainerSystem
{
    public bool TryCreateCirculatorySolution(EntityUid entity,
        string name,
        out Entity<SolutionComponent> solution)
    {
        if (TryGetSolution(entity, name, out Entity<SolutionComponent>? existing, out _) ||
            !TryComp(entity, out SolutionManagerComponent? manager))
        {
            solution = default;
            return false;
        }

        solution = CreateDefaultSolution((entity, manager), name);
        return true;
    }

    public bool TryDeleteCirculatorySolution(EntityUid entity, string name)
    {
        if (!TryGetSolution(entity, name, out Entity<SolutionComponent>? solution, out _))
            return false;

        Del(solution.Value.Owner);
        return true;
    }
}

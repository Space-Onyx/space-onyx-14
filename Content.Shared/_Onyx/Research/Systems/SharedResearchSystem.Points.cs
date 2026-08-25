using System.Linq;
using Content.Shared._Onyx.Research;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;

namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    /// <summary>
    /// Returns the effective typed costs of a technology, aggregating entries of the same type.
    /// Falls back to the legacy <see cref="TechnologyPrototype.Cost"/> paid in General points.
    /// </summary>
    public List<ResearchPointAmount> GetTechnologyCosts(TechnologyPrototype technology)
    {
        IReadOnlyList<ResearchPointAmount> costs = technology.PointCosts.Count > 0
            ? technology.PointCosts
            : new[] { new ResearchPointAmount(ResearchPointAmount.General, technology.Cost) };
        return AggregatePoints(costs);
    }

    public static List<ResearchPointAmount> AggregatePoints(IEnumerable<ResearchPointAmount> amounts)
    {
        var aggregated = new Dictionary<string, int>();
        foreach (var amount in amounts)
        {
            aggregated.TryGetValue(amount.Type, out var existing);
            aggregated[amount.Type] = existing + amount.Amount;
        }

        return aggregated
            .Select(pair => new ResearchPointAmount(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    /// Checks whether the given balances cover the costs of a technology.
    /// </summary>
    public bool CanAffordTechnology(IReadOnlyList<ResearchPointAmount> balances, TechnologyPrototype technology)
    {
        foreach (var cost in GetTechnologyCosts(technology))
        {
            if (GetPointAmount(balances, cost.Type) < cost.Amount)
                return false;
        }

        return true;
    }

    public static int GetPointAmount(IReadOnlyList<ResearchPointAmount> amounts, string type)
    {
        foreach (var amount in amounts)
        {
            if (amount.Type == type)
                return amount.Amount;
        }

        return 0;
    }

    /// <summary>
    /// Builds a player facing text of a technology's costs.
    /// </summary>
    public string FormatTechnologyCosts(TechnologyPrototype technology)
    {
        var costs = GetTechnologyCosts(technology);
        if (costs.Count == 1 && costs[0].Type == ResearchPointAmount.General)
            return Loc.GetString("research-console-cost", ("amount", costs[0].Amount));

        var entries = costs.Select(cost => Loc.GetString("research-console-point-cost-entry",
            ("type", GetPointTypeName(cost.Type)),
            ("amount", cost.Amount)));
        return string.Join(", ", entries);
    }

    public string GetPointTypeName(string type)
    {
        return ProtoMan.TryIndex<ResearchPointTypePrototype>(type, out var prototype)
            ? Loc.GetString(prototype.Name)
            : type;
    }
}

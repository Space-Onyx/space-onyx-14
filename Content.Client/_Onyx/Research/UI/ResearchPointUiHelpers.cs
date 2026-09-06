using System.Linq;
using Content.Shared._Onyx.Research;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Research.UI;

public static class ResearchPointUiHelpers
{
    public static string BuildBalanceMarkup(IReadOnlyList<ResearchPointAmount> balances, SharedResearchSystem research, IPrototypeManager prototypes)
    {
        return string.Join(" | ", balances.Select(balance => BuildEntryMarkup(
            balance.Amount,
            GetColor(balance.Type, prototypes),
            research.GetPointTypeName(balance.Type))));
    }

    public static string BuildCostMarkup(TechnologyPrototype technology, SharedResearchSystem research, IPrototypeManager prototypes, TechnologyDatabaseComponent? database = null)
    {
        return string.Join(", ", research.GetTechnologyCosts(technology, database).Select(cost => BuildEntryMarkup(
            cost.Amount,
            GetColor(cost.Type, prototypes),
            research.GetPointTypeName(cost.Type))));
    }

    /// <summary>
    /// Builds compact colored markup of typed amounts using their abbreviations, e.g. "4000 О.И + 220 Э.И".
    /// </summary>
    public static string BuildAbbreviatedBalanceMarkup(IReadOnlyList<ResearchPointAmount> amounts, SharedResearchSystem research, IPrototypeManager prototypes)
    {
        return string.Join(" + ", SharedResearchSystem.AggregatePoints(amounts).Select(cost => Loc.GetString(
            "research-point-entry-markup",
            ("color", GetColor(cost.Type, prototypes).ToHex()),
            ("amount", cost.Amount),
            ("abbreviation", research.GetPointTypeAbbreviation(cost.Type)))));
    }

    private static string BuildEntryMarkup(int amount, Color color, string name)
    {
        return Loc.GetString("research-console-point-entry-markup",
            ("color", color.ToHex()),
            ("amount", amount),
            ("type", name));
    }

    private static Color GetColor(string type, IPrototypeManager prototypes)
    {
        return prototypes.TryIndex<ResearchPointTypePrototype>(type, out var prototype)
            ? prototype.Color
            : Color.White;
    }
}

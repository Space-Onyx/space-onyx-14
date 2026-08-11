using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Botany.PlantAnalyzer;

public sealed partial class PlantAnalyzerLocalizationHelper : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string GasesToLocalizedStrings(List<Gas> gases)
    {
        if (gases.Count == 0)
            return "";

        var ids = gases.Select(gas => (int) gas).ToHashSet();
        var localized = _prototypeManager.EnumeratePrototypes<GasPrototype>()
            .Where(gas => ids.Contains(int.Parse(gas.ID)))
            .Select(gas => Loc.GetString(gas.Name))
            .ToList();
        return ContentLocalizationManager.FormatListLocalized(localized, "plant-analyzer-and");
    }

    public string ChemicalsToLocalizedStrings(List<string> ids)
    {
        var localized = ids.Select(id => _prototypeManager.TryIndex<ReagentPrototype>(id, out var prototype)
                ? prototype.LocalizedName
                : id)
            .ToList();
        return ContentLocalizationManager.FormatListLocalized(localized, "plant-analyzer-and");
    }

    public (string Singular, string Plural, string First) ProduceToLocalizedStrings(List<EntProtoId> ids)
    {
        if (ids.Count == 0)
            return ("", "", "");

        var singular = new List<string>();
        var plural = new List<string>();
        foreach (var id in ids)
        {
            var name = _prototypeManager.TryIndex(id, out var prototype) ? prototype.Name : id.Id;
            singular.Add(name);
            plural.Add(Loc.GetString("plant-analyzer-produce-plural", ("thing", name)));
        }

        return (ContentLocalizationManager.FormatListToOr(singular),
            ContentLocalizationManager.FormatListToOr(plural),
            singular[0]);
    }
}

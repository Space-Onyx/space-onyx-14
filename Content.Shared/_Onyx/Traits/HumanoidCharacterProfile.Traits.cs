// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Traits;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public HumanoidCharacterProfile WithTraitPreference(ProtoId<TraitPrototype> traitId, IPrototypeManager protoManager, IConfigurationManager configManager)
    {
        // null category is assumed to be default.
        if (!protoManager.TryIndex(traitId, out var traitProto))
            return new(this);

        foreach (var selectedId in _traitPreferences)
        {
            if (!protoManager.TryIndex(selectedId, out var selectedProto))
                continue;

            if (traitProto.Conflicts.Contains(selectedId) || selectedProto.Conflicts.Contains(traitId))
                return new(this);
        }

        var category = traitProto.Category;

        // Category not found so dump it.
        TraitCategoryPrototype? traitCategory = null;

        if (category != null && !protoManager.Resolve(category, out traitCategory))
            return new(this);

        var list = new HashSet<ProtoId<TraitPrototype>>(_traitPreferences) { traitId };

        var totalPoints = 0;
        var categoryCount = 0;
        foreach (var selectedId in list)
        {
            if (!protoManager.TryIndex(selectedId, out var selectedProto))
                continue;

            totalPoints += selectedProto.Cost;
            if (selectedProto.Category == category)
                categoryCount++;
        }

        if (totalPoints > Math.Max(0, configManager.GetCVar(CCVars.MaxTraitPoints)) ||
            traitCategory?.MaxTraits is { } maxTraits && categoryCount > maxTraits)
        {
            return new(this);
        }

        if (traitCategory == null || traitCategory.MaxTraitPoints < 0)
        {
            return new(this)
            {
                _traitPreferences = list,
            };
        }

        var count = 0;
        foreach (var trait in list)
        {
            // If trait not found or another category don't count its points.
            if (!protoManager.TryIndex<TraitPrototype>(trait, out var otherProto) ||
                otherProto.Category != traitCategory)
            {
                continue;
            }

            count += otherProto.Cost;
        }

        if (count > traitCategory.MaxTraitPoints && traitProto.Cost != 0)
        {
            return new(this);
        }

        return new(this)
        {
            _traitPreferences = list,
        };
    }

    public HumanoidCharacterProfile WithoutTraitPreference(ProtoId<TraitPrototype> traitId, IPrototypeManager protoManager, IConfigurationManager configManager)
    {
        var list = new HashSet<ProtoId<TraitPrototype>>(_traitPreferences);
        list.Remove(traitId);

        var totalPoints = 0;
        foreach (var selectedId in list)
        {
            if (protoManager.TryIndex(selectedId, out var selectedProto))
                totalPoints += selectedProto.Cost;
        }

        if (totalPoints > Math.Max(0, configManager.GetCVar(CCVars.MaxTraitPoints)))
            return new(this);

        return new(this)
        {
            _traitPreferences = list,
        };
    }

    /// <summary>
    /// Takes in an IEnumerable of traits and returns a List of the valid traits.
    /// </summary>
    public List<ProtoId<TraitPrototype>> GetValidTraits(IEnumerable<ProtoId<TraitPrototype>> traits, IPrototypeManager protoManager, IConfigurationManager configManager)
    {
        // Track points count for each group.
        var groups = new Dictionary<string, int>();
        var result = new List<ProtoId<TraitPrototype>>();

        foreach (var trait in traits
                     .Select(id => (Id: id, Prototype: protoManager.TryIndex(id, out TraitPrototype? proto) ? proto : null))
                     .Where(entry => entry.Prototype is not null)
                     .OrderBy(entry => entry.Prototype!.Cost))
        {
            var traitProto = trait.Prototype!;

            var conflicts = false;
            foreach (var selectedId in result)
            {
                if (!protoManager.TryIndex(selectedId, out var selectedProto))
                    continue;

                if (traitProto.Conflicts.Contains(selectedId) || selectedProto.Conflicts.Contains(trait.Id))
                {
                    conflicts = true;
                    break;
                }
            }

            if (conflicts)
                continue;

            var totalPoints = result.Sum(id => protoManager.Index<TraitPrototype>(id).Cost) + traitProto.Cost;
            var categoryCount = result.Count(id =>
                protoManager.TryIndex(id, out TraitPrototype? selected) &&
                selected.Category == traitProto.Category);

            if (totalPoints > Math.Max(0, configManager.GetCVar(CCVars.MaxTraitPoints)))
                continue;

            if (traitProto.Category is { } categoryId &&
                protoManager.TryIndex(categoryId, out TraitCategoryPrototype? categoryLimit) &&
                categoryLimit.MaxTraits is { } maxTraits &&
                categoryCount >= maxTraits)
            {
                continue;
            }

            // Always valid.
            if (traitProto.Category == null)
            {
                result.Add(trait.Id);
                continue;
            }

            // No category so dump it.
            if (!protoManager.Resolve(traitProto.Category, out var category))
                continue;

            var existing = groups.GetOrNew(category.ID);
            existing += traitProto.Cost;

            // Too expensive.
            if (existing > category.MaxTraitPoints)
                continue;

            groups[category.ID] = existing;
            result.Add(trait.Id);
        }

        return result;
    }
}

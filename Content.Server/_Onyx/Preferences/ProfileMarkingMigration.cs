// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Preferences;

public static class ProfileMarkingMigration
{
    private static readonly (ProtoId<OrganCategoryPrototype> Source, ProtoId<OrganCategoryPrototype> Target)[] OrganMigrations =
    [
        ("Ears", "Head"),
        ("Tail", "Groin"),
        ("Wings", "Chest"),
    ];

    public static void Migrate(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        ProtoId<SpeciesPrototype> species,
        MarkingManager markingManager)
    {
        var markingData = markingManager.GetMarkingData(species);
        var supportedLayers = markingData.Values
            .SelectMany(data => data.Layers)
            .ToHashSet();

        if (!supportedLayers.Contains(HumanoidVisualLayers.Ears) &&
            supportedLayers.Contains(HumanoidVisualLayers.HeadTop))
        {
            foreach (var layers in markings.Values)
                MoveLayer(layers, HumanoidVisualLayers.Ears, HumanoidVisualLayers.HeadTop);
        }

        if (!supportedLayers.Contains(HumanoidVisualLayers.EarsOverlay) &&
            supportedLayers.Contains(HumanoidVisualLayers.HeadTop))
        {
            foreach (var layers in markings.Values)
                MoveLayer(layers, HumanoidVisualLayers.EarsOverlay, HumanoidVisualLayers.HeadTop);
        }

        foreach (var (source, target) in OrganMigrations)
        {
            if (!markingData.ContainsKey(source) && markingData.ContainsKey(target))
                MoveOrgan(markings, source, target);
        }
    }

    private static void MoveOrgan(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        ProtoId<OrganCategoryPrototype> source,
        ProtoId<OrganCategoryPrototype> target)
    {
        if (!markings.Remove(source, out var sourceLayers))
            return;

        var targetLayers = markings.GetOrNew(target);
        foreach (var (layer, sourceMarkings) in sourceLayers)
            Merge(targetLayers.GetOrNew(layer), sourceMarkings);
    }

    private static void MoveLayer(
        Dictionary<HumanoidVisualLayers, List<Marking>> markings,
        HumanoidVisualLayers source,
        HumanoidVisualLayers target)
    {
        if (markings.Remove(source, out var sourceMarkings))
            Merge(markings.GetOrNew(target), sourceMarkings);
    }

    private static void Merge(List<Marking> target, List<Marking> source)
    {
        foreach (var marking in source)
        {
            if (!target.Contains(marking))
                target.Add(marking);
        }
    }
}

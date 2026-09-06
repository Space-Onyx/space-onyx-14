// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.Parallax.Biomes;

public abstract partial class SharedBiomeSystem
{
    private static bool TryPickWeightedEntity(
        IReadOnlyDictionary<EntProtoId, float> entities,
        float value,
        out EntProtoId entity)
    {
        var totalWeight = 0f;
        foreach (var weight in entities.Values)
            totalWeight += Math.Max(0f, weight);

        if (totalWeight <= 0f)
        {
            entity = default;
            return false;
        }

        var remainingWeight = Math.Clamp(value, 0f, 1f) * totalWeight;
        EntProtoId fallback = default;
        foreach (var (prototype, weight) in entities)
        {
            if (weight <= 0f)
                continue;

            fallback = prototype;
            remainingWeight -= weight;
            if (remainingWeight <= 0f)
            {
                entity = prototype;
                return true;
            }
        }

        entity = fallback;
        return true;
    }
}

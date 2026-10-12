// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Kitchen.Components;

namespace Content.Shared.Kitchen.EntitySystems;

public abstract partial class MicrowaveSystem
{
    public void SetMachinePartEffects(
        Entity<MicrowaveComponent> ent,
        float cookTimeMultiplier,
        int capacity,
        float explosionChance)
    {
        ent.Comp.CookTimeMultiplier = cookTimeMultiplier;
        ent.Comp.Capacity = capacity;
        ent.Comp.ExplosionChance = explosionChance;
        Dirty(ent);
    }
}

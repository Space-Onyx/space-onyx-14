// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Clothing;

public sealed partial class CleanDirtEntityEffectSystem
    : EntityEffectSystem<ClothingDirtableComponent, CleanDirt>
{
    [Dependency] private ClothingDirtSystem _dirt = default!;

    protected override void Effect(Entity<ClothingDirtableComponent> entity, ref EntityEffectEvent<CleanDirt> args)
    {
        _dirt.TryCleanDirt(entity, args.Effect.Multiplier * args.Scale, entity.Comp);
    }
}

public sealed partial class CleanDirt : EntityEffectBase<CleanDirt>
{
    [DataField]
    public float Multiplier = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-clean-dirt",
            ("chance", Probability),
            ("multiplier", Multiplier));
}

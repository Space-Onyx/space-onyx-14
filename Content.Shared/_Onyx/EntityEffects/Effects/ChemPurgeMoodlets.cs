// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Mood;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.EntityEffects.Effects;

/// <summary>
///     Metabolism effect clearing timed moodlets in bulk.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemPurgeMoodletsSystem : EntityEffectSystem<MetaDataComponent, ChemPurgeMoodlets>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemPurgeMoodlets> args)
    {
        RaiseLocalEvent(entity, new MoodPurgeEffectsEvent(args.Effect.RemovePermanentMoodlets));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemPurgeMoodlets : EntityEffectBase<ChemPurgeMoodlets>
{
    [DataField]
    public bool RemovePermanentMoodlets;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-purge-moodlets");
}

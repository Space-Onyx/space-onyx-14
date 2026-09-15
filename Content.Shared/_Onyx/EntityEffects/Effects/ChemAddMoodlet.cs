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
///     Metabolism effect applying a moodlet to the metabolizing entity.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemAddMoodletSystem : EntityEffectSystem<MetaDataComponent, ChemAddMoodlet>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemAddMoodlet> args)
    {
        RaiseLocalEvent(entity, new MoodEffectEvent(args.Effect.MoodPrototype));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemAddMoodlet : EntityEffectBase<ChemAddMoodlet>
{
    /// <summary>
    ///     Moodlet applied while the reagent metabolizes.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var moodPrototype = prototype.Index<MoodEffectPrototype>(MoodPrototype.Id);
        return Loc.GetString("reagent-effect-guidebook-add-moodlet",
            ("amount", moodPrototype.MoodChange),
            ("timeout", moodPrototype.Timeout));
    }
}

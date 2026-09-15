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
///     Metabolism effect lifting one specific moodlet.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRemoveMoodletSystem : EntityEffectSystem<MetaDataComponent, ChemRemoveMoodlet>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ChemRemoveMoodlet> args)
    {
        RaiseLocalEvent(entity, new MoodRemoveEffectEvent(args.Effect.MoodPrototype));
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ChemRemoveMoodlet : EntityEffectBase<ChemRemoveMoodlet>
{
    /// <summary>
    ///     Moodlet to lift if present.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MoodEffectPrototype> MoodPrototype;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var moodPrototype = prototype.Index<MoodEffectPrototype>(MoodPrototype.Id);
        return Loc.GetString("reagent-effect-guidebook-remove-moodlet",
            ("name", moodPrototype.Description()));
    }
}

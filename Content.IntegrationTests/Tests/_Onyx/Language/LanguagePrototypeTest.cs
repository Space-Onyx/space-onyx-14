// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server._Onyx.Traits;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Traits;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Language;

public sealed class LanguagePrototypeTest : GameTest
{
    private static readonly ProtoId<LanguagePrototype> Sign = "Sign";
    private static readonly ProtoId<LanguagePrototype> SolCommon = "SolCommon";

    [SidedDependency(Side.Server)] private IComponentFactory _componentFactory = default!;
    [SidedDependency(Side.Server)] private ILocalizationManager _localization = default!;
    [SidedDependency(Side.Server)] private IPrototypeManager _prototypes = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void LanguageReferencesAndLocalizationAreValid()
    {
        Assert.Multiple(() =>
        {
            foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryComp(out LanguageKnowledgeComponent knowledge, _componentFactory))
                    continue;

                foreach (var language in knowledge.SpokenLanguages.Concat(knowledge.UnderstoodLanguages))
                    Assert.That(_prototypes.HasIndex(language), Is.True,
                        $"Entity prototype {entity.ID} references missing language {language}.");
            }

            foreach (var language in _prototypes.EnumeratePrototypes<LanguagePrototype>())
            {
                Assert.That(_localization.HasString($"language-{language.ID}-name"), Is.True,
                    $"Language {language.ID} has no localized name.");
                Assert.That(_localization.HasString($"language-{language.ID}-description"), Is.True,
                    $"Language {language.ID} has no localized description.");
            }

            foreach (var trait in _prototypes.EnumeratePrototypes<TraitPrototype>())
            {
                foreach (var language in trait.Specials.OfType<LanguageSpecial>().SelectMany(special => special.Languages))
                    Assert.That(_prototypes.HasIndex(language), Is.True,
                        $"Trait prototype {trait.ID} references missing language {language}.");
            }
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void MultipleLanguageTraitsAreMerged()
    {
        var entity = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        new LanguageSpecial { Languages = [Sign] }.AfterEquip(entity);
        new LanguageSpecial { Languages = [SolCommon] }.AfterEquip(entity);

        var knowledge = SEntMan.GetComponent<LanguageTraitComponent>(entity);
        Assert.That(knowledge.Languages, Is.EquivalentTo(new[] { Sign, SolCommon }));
        SEntMan.DeleteEntity(entity);
    }
}

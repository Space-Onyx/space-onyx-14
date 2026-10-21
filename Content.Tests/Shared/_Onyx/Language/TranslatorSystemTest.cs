using Content.Shared._Onyx.Language;
using NUnit.Framework;

namespace Content.Tests.Shared._Onyx.Language;

[TestFixture]
public sealed class TranslatorSystemTest
{
    [Test]
    public void RequirementsRespectAnyAndAllModes()
    {
        Assert.That(Resolves(requireAll: false, knowsBoth: false), Is.True);
        Assert.That(Resolves(requireAll: true, knowsBoth: false), Is.False);
        Assert.That(Resolves(requireAll: true, knowsBoth: true), Is.True);
        Assert.That(Resolves(requireAll: true, knowsBoth: false, emptyRequirements: true), Is.True);
    }

    private static bool Resolves(bool requireAll, bool knowsBoth, bool emptyRequirements = false)
    {
        var knowledge = new CollectLanguageKnowledgeEvent();
        knowledge.Grant("TauCetiBasic", LanguageAbility.Understand);
        if (knowsBoth)
            knowledge.Grant("SolCommon", LanguageAbility.Understand);
        var translator = new HandheldTranslatorComponent
        {
            Requires = emptyRequirements ? [] : ["TauCetiBasic", "SolCommon"],
            RequiresAll = requireAll,
            Languages = new() { ["Draconic"] = LanguageAbility.Understand },
        };
        LanguageKnowledge.ResolveTranslators([translator], knowledge);
        return LanguageKnowledge.Has(knowledge.Languages, "Draconic", LanguageAbility.Understand);
    }
}

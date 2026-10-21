// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Onyx.Language;
using Content.Shared._Onyx.Language.Paper;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared._Onyx.Language;

[TestFixture]
public sealed class LanguageDocumentTest
{
    [Test]
    public void KnowledgeSourcesMergeIndependentAbilities()
    {
        var knowledge = new Dictionary<ProtoId<LanguagePrototype>, LanguageAbility>();
        LanguageKnowledge.Grant(knowledge, "SolCommon", LanguageAbility.Read);
        LanguageKnowledge.Grant(knowledge, "SolCommon", LanguageAbility.Speak);
        Assert.That(LanguageKnowledge.Has(knowledge, "SolCommon", LanguageAbility.Read | LanguageAbility.Speak), Is.True);
        Assert.That(LanguageKnowledge.Has(knowledge, "SolCommon", LanguageAbility.Write), Is.False);
        Assert.That(LanguageKnowledge.Grant(knowledge, "SolCommon", LanguageAbility.Read), Is.False);
    }

    [Test]
    public void EmptyDocumentAcceptsInsertion()
    {
        var spans = new List<LanguageTextSpan>();
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(0, 0, "text"), "TauCetiBasic"), Is.True);
        Assert.That(spans.Single().SourceText, Is.EqualTo("text"));
    }

    [Test]
    public void TranslatorCyclesNeedASeedAndResolveEveryModality()
    {
        var first = new HandheldTranslatorComponent
        {
            TranslateWriting = true,
            Requires = ["SolCommon"],
            Languages = new() { ["Draconic"] = LanguageAbility.All },
        };
        var second = new HandheldTranslatorComponent
        {
            TranslateWriting = true,
            Requires = ["Draconic"],
            Languages = new() { ["SolCommon"] = LanguageAbility.All },
        };
        var knowledge = new CollectLanguageKnowledgeEvent();
        LanguageKnowledge.ResolveTranslators([first, second], knowledge);
        Assert.That(knowledge.Languages, Is.Empty);
        knowledge.Grant("SolCommon", LanguageAbility.Read);
        LanguageKnowledge.ResolveTranslators([second, first], knowledge);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "Draconic", LanguageAbility.Read), Is.True);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "Draconic", LanguageAbility.Speak), Is.False);
        knowledge.Grant("SolCommon", LanguageAbility.Speak);
        LanguageKnowledge.ResolveTranslators([second, first], knowledge);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "Draconic", LanguageAbility.Speak), Is.True);
    }

    [Test]
    public void OpaqueEditIsAtomicAndTransactional()
    {
        var spans = new List<LanguageTextSpan> { new("unknown", "secret", "SolCommon", false) };
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(1, 1, "x"), "TauCetiBasic"), Is.False);
        Assert.That(spans.Single().SourceText, Is.EqualTo("secret"));
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(0, 7, "known"), "TauCetiBasic"), Is.True);
        Assert.That(spans.Single().SourceText, Is.EqualTo("known"));
        Assert.That(spans.Single().Language.Id, Is.EqualTo("TauCetiBasic"));
    }

    [Test]
    public void InsertionsAtOpaqueBoundariesPreserveSource()
    {
        var spans = new List<LanguageTextSpan> { new("unknown", "secret", "SolCommon", false) };
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(7, 0, " end"), "TauCetiBasic"), Is.True);
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(0, 0, "start "), "TauCetiBasic"), Is.True);
        Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo("start secret end"));
    }

    [Test]
    public void KnownEditPreservesNeighborLanguages()
    {
        var spans = new List<LanguageTextSpan> { new("one two", "one two", "TauCetiBasic", true) };
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(4, 3, "two"), "SolCommon"), Is.True);
        Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo("one two"));
        Assert.That(spans[0].Language.Id, Is.EqualTo("TauCetiBasic"));
        Assert.That(spans[^1].Language.Id, Is.EqualTo("SolCommon"));
    }

    [Test]
    public void InvalidOffsetsAndUtf16AreRejected()
    {
        var spans = new List<LanguageTextSpan> { new("😀text", "😀text", "SolCommon", true) };
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(1, 0, "x"), "SolCommon"), Is.False);
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(0, int.MaxValue, ""), "SolCommon"), Is.False);
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(0, 0, "\uD800"), "SolCommon"), Is.False);
        Assert.That(spans.Single().SourceText, Is.EqualTo("😀text"));
    }

    [Test]
    public void NormalizePreservesCoverageWithoutOverflow()
    {
        var ranges = new List<LanguageTextRange> { new(0, 3, "TauCetiBasic"), new(2, 3, "SolCommon"), new(7, int.MaxValue, "Draconic") };
        LanguageDocument.Normalize(ranges, 9);
        Assert.That(LanguageDocument.Covers(ranges, 9), Is.True);
        Assert.That(ranges.Select(range => range.Start), Is.EqualTo(new[] { 0, 3, 5, 7 }));
        Assert.That(ranges.Select(range => range.Length), Is.EqualTo(new[] { 3, 2, 2, 2 }));
    }

    [Test]
    public void ProjectionProtectsMarkupAcrossLanguageBoundaries()
    {
        const string text = "[bold]secret[/bold]\nnext";
        var ranges = new List<LanguageTextRange> { new(0, 3, "TauCetiBasic"), new(3, text.Length - 3, "SolCommon") };
        var spans = LanguageDocument.Project(text, ranges, _ => false, (_, _) => "?", PaperLanguageMarkup.GetProtectedLength);
        Assert.That(string.Concat(spans.Select(span => span.ViewText)), Is.EqualTo("[bold]?[/bold]\n?"));
        Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo(text));
        Assert.That(spans.Where(span => !span.Exact).All(span => span.ViewText != span.SourceText), Is.True);
    }

    [TestCase("[bold]", true)]
    [TestCase("[/color]", true)]
    [TestCase("[color=#ff0000]", true)]
    [TestCase("[secret]", false)]
    [TestCase("[bold=secret]", false)]
    [TestCase("[bold secret=message]", false)]
    [TestCase("[color=secret]", false)]
    [TestCase("[/color=secret]", false)]
    [TestCase("[font=secret]", false)]
    [TestCase("[paperlang=Universal]", false)]
    [TestCase("\\[bold]", false)]
    public void OnlyAllowedUnescapedMarkupIsProtected(string text, bool expected)
    {
        var offset = text.StartsWith('\\') ? 1 : 0;
        Assert.That(PaperLanguageMarkup.IsAllowedTag(text, offset), Is.EqualTo(expected));
    }

    [Test]
    public void SyllableObfuscationPreservesStructureAndCase()
    {
        var method = new SyllableObfuscation { Replacement = ["ka", "ri", "to"], MinSyllables = 1, MaxSyllables = 4 };
        var result = method.Obfuscate("HELLO, World 52!", 7);
        Assert.That(result, Does.Match("^[A-Z]+, [A-Z][a-z]+ 52!$"));
        Assert.That(result, Is.EqualTo(method.Obfuscate("HELLO, World 52!", 7)));
    }

    [Test]
    public void ReplacementObfuscationPreservesOpaqueActions()
    {
        var obfuscation = new ReplacementObfuscation { Replacement = ["?"] };
        Assert.That(LanguageText.Obfuscate("hello \uFFFC0\uFFFC world", obfuscation, 7, LanguageText.InlineActionLength),
            Is.EqualTo("?\uFFFC0\uFFFC?"));
        Assert.That(LanguageText.InlineActionLength("\uFFFC\uFFFC0\uFFFC\uFFFC", 0), Is.EqualTo(5));
    }

    [Test]
    public void HandheldDoesNotGrantLiteracyUnlessConfigured()
    {
        var translator = new HandheldTranslatorComponent
        {
            Languages = new() { ["SolCommon"] = LanguageAbility.All },
        };
        var knowledge = new CollectLanguageKnowledgeEvent();
        LanguageKnowledge.ResolveTranslators([translator], knowledge);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "SolCommon", LanguageAbility.Speak), Is.True);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "SolCommon", LanguageAbility.Read), Is.False);
        translator.TranslateWriting = true;
        LanguageKnowledge.ResolveTranslators([translator], knowledge);
        Assert.That(LanguageKnowledge.Has(knowledge.Languages, "SolCommon", LanguageAbility.Read | LanguageAbility.Write), Is.True);
    }

    [Test]
    public void RejectedEditAfterKnownPrefixDoesNotMutateDocument()
    {
        var spans = new List<LanguageTextSpan>
        {
            new("known", "known", "TauCetiBasic", true),
            new("unknown", "secret", "SolCommon", false),
        };
        var original = spans.ToArray();
        Assert.That(LanguageDocument.TryEdit(spans, new LanguageTextEdit(2, 5, "x"), "TauCetiBasic"), Is.False);
        Assert.That(spans, Is.EqualTo(original));
    }

    [Test]
    public void ProjectionPreservesEscapedLiteralAndNestedFormatting()
    {
        const string text = "[bold][italic]secret[/italic][/bold]";
        var spans = LanguageDocument.Project(text, LanguageDocument.ForText(text, "SolCommon"),
            _ => false, (_, _) => "?", PaperLanguageMarkup.GetProtectedLength);
        Assert.That(string.Concat(spans.Select(span => span.ViewText)), Is.EqualTo("[bold][italic]?[/italic][/bold]"));
        Assert.That(PaperLanguageMarkup.GetProtectedLength("\\[bold]", 1), Is.Zero);
        Assert.That(PaperLanguageMarkup.GetProtectedLength("\\\\[bold]", 2), Is.EqualTo(6));
        Assert.That(PaperLanguageMarkup.GetProtectedLength("[paperlang=Universal]", 0), Is.Zero);
    }

    [TestCase("[secret message]")]
    [TestCase("[bold=secret message]")]
    [TestCase("[color=secret]")]
    [TestCase("[font=secret]")]
    public void BracketsAndInvalidTagParametersNeverExposeSource(string text)
    {
        var spans = LanguageDocument.Project(text, LanguageDocument.ForText(text, "SolCommon"),
            _ => false, (_, _) => "?", PaperLanguageMarkup.GetProtectedLength);
        Assert.That(string.Concat(spans.Select(span => span.ViewText)), Is.EqualTo("?"));
        Assert.That(spans.All(span => !span.Exact), Is.True);
    }
}

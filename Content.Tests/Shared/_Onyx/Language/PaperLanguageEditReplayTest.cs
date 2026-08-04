using System.Collections.Generic;
using System.Linq;
using Content.Shared._Onyx.Language.Paper;
using NUnit.Framework;

namespace Content.Tests.Shared._Onyx.Language;

[TestFixture]
public sealed class PaperLanguageEditReplayTest
{
    [Test]
    public void EmptyDocumentAcceptsInsertion()
    {
        var spans = new List<PaperLanguageEditSpan>();

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(0, 0, "text"), "TauCetiBasic"), Is.True);
        Assert.That(spans.Single().SourceText, Is.EqualTo("text"));
    }

    [Test]
    public void OverflowRangeIsRejected()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("text", "text", "TauCetiBasic", true),
        };

        Assert.That(PaperLanguageEditReplay.Apply(
            spans,
            new PaperLanguageEditOperation(1, int.MaxValue, string.Empty),
            "TauCetiBasic"), Is.False);
    }

    [Test]
    public void ReplacingKnownWordPreservesNeighbors()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("first", "first", "TauCetiBasic", true),
            new(" ", " ", "TauCetiBasic", true),
            new("second", "second", "SolCommon", true),
        };

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(6, 6, "changed"), "SintaUnathi"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo("first changed"));
            Assert.That(spans[0].Language.Id, Is.EqualTo("TauCetiBasic"));
            Assert.That(spans[^1].Language.Id, Is.EqualTo("SintaUnathi"));
        });
    }

    [Test]
    public void UnknownSpanIsAtomic()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("gibberish", "secret", "SolCommon", false),
        };

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(1, 1, "x"), "TauCetiBasic"), Is.False);
        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(0, 9, "known"), "TauCetiBasic"), Is.True);
        Assert.That(spans.Single().Language.Id, Is.EqualTo("TauCetiBasic"));
    }

    [Test]
    public void InsertingAfterUnknownSpanKeepsOrder()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("gibberish", "secret", "SolCommon", false),
        };

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(9, 0, " added"), "TauCetiBasic"), Is.True);
        Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo("secret added"));
    }

    [Test]
    public void InsertingInsideUnknownSpanIsRejectedWithoutMutation()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("gibberish", "secret", "SolCommon", false),
        };

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(4, 0, "x"), "TauCetiBasic"), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(spans.Single().SourceText, Is.EqualTo("secret"));
            Assert.That(spans.Single().Language.Id, Is.EqualTo("SolCommon"));
        });
    }

    [Test]
    public void IdenticalRewriteChangesOnlySelectedLanguage()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("one two", "one two", "TauCetiBasic", true),
        };

        Assert.That(PaperLanguageEditReplay.Apply(spans, new PaperLanguageEditOperation(4, 3, "two"), "SolCommon"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(spans.Select(span => span.SourceText)), Is.EqualTo("one two"));
            Assert.That(spans[0].Language.Id, Is.EqualTo("TauCetiBasic"));
            Assert.That(spans[^1].Language.Id, Is.EqualTo("SolCommon"));
        });
    }

    [Test]
    public void SeparatorUsesFollowingLanguage()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("first", "first", "TauCetiBasic", true),
            new(" ", " ", "Universal", true),
            new("second", "second", "SolCommon", true),
        };

        PaperLanguageEditReplay.CanonicalizeSeparators(spans);

        Assert.That(spans[1].Language.Id, Is.EqualTo("SolCommon"));
    }

    [Test]
    public void SeparatorChainUsesFollowingLanguage()
    {
        var spans = new List<PaperLanguageEditSpan>
        {
            new("first", "first", "TauCetiBasic", true),
            new(" ", " ", "Universal", true),
            new("!", "!", "Universal", true),
            new("second", "second", "SolCommon", true),
        };

        PaperLanguageEditReplay.CanonicalizeSeparators(spans);

        Assert.That(spans.Skip(1).Take(2).All(span => span.Language.Id == "SolCommon"), Is.True);
    }

    [Test]
    public void NormalizePreservesCoordinatesAndFillsGaps()
    {
        var segments = new List<PaperLanguageSegment>
        {
            new(0, 3, "TauCetiBasic"),
            new(2, 3, "SolCommon"),
            new(7, 2, "SintaUnathi"),
        };

        PaperLanguageSegments.Normalize(segments, 9);

        Assert.Multiple(() =>
        {
            Assert.That(segments.Select(segment => segment.Start), Is.EqualTo(new[] { 0, 3, 5, 7 }));
            Assert.That(segments.Select(segment => segment.Length), Is.EqualTo(new[] { 3, 2, 2, 2 }));
            Assert.That(segments.Select(segment => segment.Language.Id),
                Is.EqualTo(new[] { "TauCetiBasic", "SolCommon", "Universal", "SintaUnathi" }));
        });
    }

    [Test]
    public void NormalizeRejectsOverflowWithoutLosingCoverage()
    {
        var segments = new List<PaperLanguageSegment>
        {
            new(0, int.MaxValue, "TauCetiBasic"),
        };

        PaperLanguageSegments.Normalize(segments, 8);

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Count.EqualTo(1));
            Assert.That(segments[0].Start, Is.Zero);
            Assert.That(segments[0].Length, Is.EqualTo(8));
        });
    }

    [TestCase("[bold]", true)]
    [TestCase("[/color]", true)]
    [TestCase("[color=#ff0000]", true)]
    [TestCase("[secret]", false)]
    [TestCase("[paperlang=\"Universal\"]", false)]
    [TestCase("plain text", false)]
    public void OnlyPaperMarkupIsProtectedFromObfuscation(string text, bool expected)
    {
        Assert.That(PaperLanguageMarkup.IsAllowedTag(text, 0), Is.EqualTo(expected));
    }
}

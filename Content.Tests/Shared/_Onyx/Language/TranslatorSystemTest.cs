using System;
using System.Collections.Generic;
using Content.Server._Onyx.Language;
using NUnit.Framework;

namespace Content.Tests.Shared._Onyx.Language;

[TestFixture]
public sealed class TranslatorSystemTest
{
    [Test]
    public void RequirementsRespectAnyAndAllModes()
    {
        var required = new HashSet<string> { "A", "B" };

        Assert.Multiple(() =>
        {
            Assert.That(TranslatorSystem.RequirementsMet(required, new HashSet<string> { "A" }, false), Is.True);
            Assert.That(TranslatorSystem.RequirementsMet(required, new HashSet<string> { "A" }, true), Is.False);
            Assert.That(TranslatorSystem.RequirementsMet(required, required, true), Is.True);
            Assert.That(TranslatorSystem.RequirementsMet(Array.Empty<string>(), Array.Empty<string>(), true), Is.True);
        });
    }
}

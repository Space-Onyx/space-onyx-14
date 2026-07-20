using Content.Shared._Onyx.Targeting;
using NUnit.Framework;

namespace Content.Tests.Shared._Onyx.Targeting;

[TestFixture]
public sealed class PartStatusTest
{
    [TestCase(0f, PartDamageSeverity.None)]
    [TestCase(1f, PartDamageSeverity.Minor)]
    [TestCase(15f, PartDamageSeverity.Moderate)]
    [TestCase(40f, PartDamageSeverity.Severe)]
    [TestCase(70f, PartDamageSeverity.Critical)]
    public void StableSeveritySnapshot(float damage, PartDamageSeverity expected)
    {
        Assert.That(PartStatusSystem.GetSeverity(damage), Is.EqualTo(expected));
        Assert.That(PartStatusSystem.Missing.Exists, Is.False);
    }
}

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Stealth.ForcedStealth;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.IntegrationTests.Tests._Onyx.Stealth;

[TestOf(typeof(ForcedStealthSystem))]
public sealed class ForcedStealthTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly SharedStealthSystem _stealth = null!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxForcedStealthTarget
  components:
  - type: MobState
  - type: Stealth
    enabled: false
    lastVisibility: 0.75

- type: entity
  parent: MobStatusEffectDebuff
  id: TestOnyxForcedStealthHalf
  components:
  - type: ForcedStealthStatusEffect
    visibility: 0.5
";

    [Test]
    [RunOnSide(Side.Server)]
    public void RestoresExistingStealth()
    {
        var target = SSpawn("TestOnyxForcedStealthTarget");
        var stealth = SComp<StealthComponent>(target);
        var statuses = SEntMan.System<StatusEffectsSystem>();

        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "StatusEffectOnyxForcedStealth",
            out var status,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(stealth.Enabled, Is.True);
            Assert.That(_stealth.GetVisibility(target, stealth), Is.Zero);
        });

        SEntMan.DeleteEntity(status.Value);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<StealthComponent>(target), Is.True);
            Assert.That(stealth.Enabled, Is.False);
        });

        _stealth.SetEnabled(target, true, stealth);
        Assert.That(_stealth.GetVisibility(target, stealth), Is.EqualTo(0.75f));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void OverlappingOverridesRestoreAfterLastRemoval()
    {
        var target = SSpawn("TestOnyxForcedStealthTarget");
        var stealth = SComp<StealthComponent>(target);
        var statuses = SEntMan.System<StatusEffectsSystem>();

        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "StatusEffectOnyxForcedStealth",
            out var hidden,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(statuses.TryAddStatusEffectDuration(target,
            "TestOnyxForcedStealthHalf",
            out var half,
            TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(_stealth.GetVisibility(target, stealth), Is.EqualTo(0.5f));

        SEntMan.DeleteEntity(hidden.Value);
        Assert.Multiple(() =>
        {
            Assert.That(stealth.Enabled, Is.True);
            Assert.That(_stealth.GetVisibility(target, stealth), Is.EqualTo(0.5f));
        });

        SEntMan.DeleteEntity(half.Value);
        Assert.That(stealth.Enabled, Is.False);

        _stealth.SetEnabled(target, true, stealth);
        Assert.That(_stealth.GetVisibility(target, stealth), Is.EqualTo(0.75f));
    }
}

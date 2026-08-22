using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Mobs.Growth;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Mobs;

[TestOf(typeof(MobGrowthSystem))]
public sealed class MobGrowthTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxGrowingMob
  components:
  - type: MobState
  - type: Satiation
    satiations:
      Hunger:
        prototype: SimpleMobBaseHunger
  - type: Appearance
  - type: MobGrowth
    initialStage: baby
    hungerRequired: 75
    hungerCost: 25
    growthInterval: 1
    stages:
      baby:
        nextStage: juvenile
      juvenile:
        nextStage: adult
      adult: {}
";

    [SidedDependency(Side.Server)] private readonly SatiationSystem _satiation = null!;
    [SidedDependency(Side.Server)] private readonly MobGrowthSystem _growth = null!;

    [Test]
    [RunOnSide(Side.Server)]
    public async Task GrowthRequiresAndConsumesHunger()
    {
        var uid = SSpawn("TestOnyxGrowingMob");
        var growth = SComp<MobGrowthComponent>(uid);
        var satiation = SComp<SatiationComponent>(uid);

        Assert.That(growth.CurrentStage, Is.EqualTo("baby"));

        _satiation.SetValue((uid, satiation), SatiationSystem.Hunger, 74f);
        Assert.That(_growth.TryGrow((uid, growth), satiation), Is.False);
        Assert.That(growth.CurrentStage, Is.EqualTo("baby"));

        _satiation.SetValue((uid, satiation), SatiationSystem.Hunger, 100f);
        Assert.That(_growth.TryGrow((uid, growth), satiation), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(growth.CurrentStage, Is.EqualTo("juvenile"));
            Assert.That(_satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger), Is.EqualTo(75f).Within(0.01f));
            Assert.That(_growth.IsInitialStage((uid, growth)), Is.False);
        });

        Assert.That(_growth.TryGrow((uid, growth), satiation), Is.True);
        Assert.That(growth.CurrentStage, Is.EqualTo("adult"));
        Assert.That(_growth.TryGrow((uid, growth), satiation), Is.False);
    }
}

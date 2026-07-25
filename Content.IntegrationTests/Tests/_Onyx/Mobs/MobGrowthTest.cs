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
  - type: Hunger
    baseDecayRate: 0
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

    [SidedDependency(Side.Server)] private readonly HungerSystem _hunger = null!;
    [SidedDependency(Side.Server)] private readonly MobGrowthSystem _growth = null!;

    [Test]
    [RunOnSide(Side.Server)]
    public async Task GrowthRequiresAndConsumesHunger()
    {
        var uid = SSpawn("TestOnyxGrowingMob");
        var growth = SComp<MobGrowthComponent>(uid);
        var hunger = SComp<HungerComponent>(uid);

        Assert.That(growth.CurrentStage, Is.EqualTo("baby"));

        _hunger.SetHunger(uid, 74f, hunger);
        Assert.That(_growth.TryGrow((uid, growth), hunger), Is.False);
        Assert.That(growth.CurrentStage, Is.EqualTo("baby"));

        _hunger.SetHunger(uid, 100f, hunger);
        Assert.That(_growth.TryGrow((uid, growth), hunger), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(growth.CurrentStage, Is.EqualTo("juvenile"));
            Assert.That(_hunger.GetHunger(hunger), Is.EqualTo(75f).Within(0.01f));
            Assert.That(_growth.IsInitialStage((uid, growth)), Is.False);
        });

        Assert.That(_growth.TryGrow((uid, growth), hunger), Is.True);
        Assert.That(growth.CurrentStage, Is.EqualTo("adult"));
        Assert.That(_growth.TryGrow((uid, growth), hunger), Is.False);
    }
}

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Xenobiology.Machines;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(SlimeGrinderSystem))]
public sealed class SlimeGrinderTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public async Task QueuesExactYieldIndependentlyAndRejectsLivingSlime()
    {
        var map = await Pair.CreateTestMap();
        var firstGrinder = SEntMan.SpawnEntity(null, map.GridCoords);
        var secondGrinder = SEntMan.SpawnEntity(null, map.GridCoords);
        var firstComponent = SEntMan.AddComponent<SlimeGrinderComponent>(firstGrinder);
        var secondComponent = SEntMan.AddComponent<SlimeGrinderComponent>(secondGrinder);
        SEntMan.System<SharedTransformSystem>().AnchorEntity(firstGrinder);
        SEntMan.System<SharedTransformSystem>().AnchorEntity(secondGrinder);

        var firstSlime = SEntMan.SpawnEntity("MobSlimeXenobioBabyGrey", map.GridCoords);
        var secondSlime = SEntMan.SpawnEntity("MobSlimeXenobioBabyGrey", map.GridCoords);
        var livingSlime = SEntMan.SpawnEntity("MobSlimeXenobioBabyGrey", map.GridCoords);
        Configure(firstSlime, 2);
        Configure(secondSlime, 3);
        Configure(livingSlime, 4);
        var mobState = SEntMan.System<MobStateSystem>();
        mobState.ChangeMobState(firstSlime, MobState.Dead);
        mobState.ChangeMobState(secondSlime, MobState.Dead);

        var system = SEntMan.System<SlimeGrinderSystem>();
        Assert.Multiple(() =>
        {
            Assert.That(system.TryQueueProcess((firstGrinder, firstComponent), firstSlime), Is.True);
            Assert.That(system.TryQueueProcess((secondGrinder, secondComponent), secondSlime), Is.True);
            Assert.That(system.TryQueueProcess((firstGrinder, firstComponent), livingSlime), Is.False);
            Assert.That(firstComponent.YieldQueue["MobMonkey"], Is.EqualTo(2));
            Assert.That(secondComponent.YieldQueue["MobMonkey"], Is.EqualTo(3));
            Assert.That(firstComponent.ProcessingTimer, Is.GreaterThan(0f));
            Assert.That(secondComponent.ProcessingTimer, Is.GreaterThan(0f));
        });
    }

    private void Configure(EntityUid slime, int yield)
    {
        var component = SComp<XenobioSlimeComponent>(slime);
        component.ProducedExtract = "MobMonkey";
        component.ExtractsProduced = yield;
    }
}

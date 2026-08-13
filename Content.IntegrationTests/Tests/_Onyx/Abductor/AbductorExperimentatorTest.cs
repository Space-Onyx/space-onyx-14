using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Abductor;
using Content.Shared.Body;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Onyx.Abductor;

[TestOf(typeof(SharedAbductorExperimentatorSystem))]
public sealed class AbductorExperimentatorTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: TestAbductorExperimentator
  components:
  - type: AbductorExperimentator
  - type: ContainerContainer
    containers:
      storage: !type:ContainerSlot
      auxiliary: !type:ContainerSlot
""";

    [Test]
    [RunOnSide(Side.Server)]
    public void StorageOnlyAcceptsMarkedBodies()
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        var experimentator = SSpawn("TestAbductorExperimentator");
        var item = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var ghostLikeMob = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.AddComponent<MobStateComponent>(ghostLikeMob);
        var victim = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.AddComponent<MobStateComponent>(victim);
        SEntMan.AddComponent<BodyComponent>(victim);
        SEntMan.AddComponent<AbductorVictimComponent>(victim);

        Assert.That(containers.TryGetContainer(experimentator, "storage", out var storage), Is.True);
        Assert.That(containers.TryGetContainer(experimentator, "auxiliary", out var auxiliary), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(containers.Insert(item, storage), Is.False);
            Assert.That(containers.Insert(item, auxiliary), Is.True);
            Assert.That(containers.Insert(ghostLikeMob, storage), Is.False);
            Assert.That(containers.Insert(victim, storage), Is.True);
        });
    }
}

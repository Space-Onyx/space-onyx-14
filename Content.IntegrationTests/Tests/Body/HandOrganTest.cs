using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(HandOrganSystem))]
public sealed class HandOrganTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TheBody
  components:
  - type: Body
  - type: Hands
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: LeftHand
        - id: RightHand

- type: entity
  id: LeftHand
  components:
  - type: Organ
  - type: HandOrgan
    handID: left
    data:
      location: Left

- type: entity
  id: RightHand
  components:
  - type: Organ
  - type: HandOrgan
    handID: right
    data:
      location: Right

- type: entity
  id: GraphBody
  components:
  - type: Body
  - type: Hands
  - type: InitialBody
    organs:
      Chest: GraphTorso
      ArmLeft: GraphArm
      HandLeft: GraphHand

- type: entity
  id: GraphTorso
  components:
  - type: BodyPart
    partType: Chest

- type: entity
  id: GraphArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Left

- type: entity
  id: GraphHand
  components:
  - type: BodyPart
    partType: Hand
    symmetry: Left
  - type: HandOrgan
    handID: left
    data:
      location: Left
";
    [Test]
    public async Task HandInsertionAndRemovalTest()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var container = entityManager.System<SharedContainerSystem>();
            var body = entityManager.SpawnEntity("TheBody", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(body);

            Assert.That(hands.Count, Is.EqualTo(2));

            var handsContainer = container.GetContainer(body, BodyComponent.ContainerID);

            var expectedCount = 2;
            var contained = handsContainer.ContainedEntities.ToList();
            foreach (var hand in contained)
            {
                expectedCount--;
                container.Remove(hand, handsContainer);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }

            var protos = new List<string>() { "LeftHand", "RightHand" };
            foreach (var proto in protos)
            {
                expectedCount++;
                entityManager.SpawnInContainerOrDrop(proto, body, BodyComponent.ContainerID);
                Assert.That(hands.Count, Is.EqualTo(expectedCount));
            }
        });
    }

    [Test]
    public async Task GraphHandRemovalTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapData = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containers = entityManager.System<SharedContainerSystem>();
            var graph = entityManager.System<SharedBodySystem>();
            var body = entityManager.SpawnEntity("GraphBody", mapData.GridCoords);
            var hands = entityManager.GetComponent<HandsComponent>(body);

            Assert.That(hands.Count, Is.EqualTo(1));
            var parts = graph.GetBodyChildren(body).ToList();
            Assert.That(parts.Count, Is.EqualTo(3));

            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;
            var arm = parts.Single(part => part.Component.PartType == BodyPartType.Arm).Id;
            var armContainer = containers.GetContainer(torso, BodyPartComponent.PartSlotPrefix + "left_arm");
            containers.Remove(arm, armContainer);

            Assert.That(hands.Count, Is.Zero);
            Assert.That(graph.GetBodyChildren(body).Count(), Is.EqualTo(1));
        });
    }
}

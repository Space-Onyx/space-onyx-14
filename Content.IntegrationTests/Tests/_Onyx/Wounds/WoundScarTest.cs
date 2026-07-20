using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundScarSystem))]
public sealed class WoundScarTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundScarBody
  parent: InventoryBase
  components:
  - type: Body
  - type: WoundHost
  - type: InitialBody
    organs:
      Torso: WoundScarTorso
      Head: WoundScarHead

- type: entity
  id: WoundScarTorso
  components:
  - type: BodyPart
    partType: Torso

- type: entity
  id: WoundScarHead
  components:
  - type: BodyPart
    partType: Head
";

    [Test]
    public async Task ThresholdTreatmentAttachmentAndRejuvenateTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WoundScarBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var wounds = entities.System<WoundSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var torso = parts.Single(candidate => candidate.Component.PartType == BodyPartType.Torso).Id;
            var part = parts.Single(candidate => candidate.Component.PartType == BodyPartType.Head).Id;
            var woundable = entities.GetComponent<WoundableComponent>(part);

            var light = wounds.CreateOrMergeWound(part, new ProtoId<WoundPrototype>("BluntWound"), 19)!.Value;
            Assert.That(wounds.CloseWound(light));
            Assert.That(wounds.GetWounds((part, woundable)).Count(HasScar), Is.Zero);
            Assert.That(wounds.RemoveWound(light));

            var heavy = wounds.CreateOrMergeWound(part, new ProtoId<WoundPrototype>("BluntWound"), 20)!.Value;
            Assert.That(wounds.CloseWound(heavy));
            var scar = wounds.GetWounds((part, woundable)).Single(HasScar);
            Assert.That(scar.Comp.State, Is.EqualTo(WoundState.Scarred));
            Assert.That(wounds.TreatWound(scar.Owner, FixedPoint2.New(1)), Is.False);
            Assert.That(wounds.RemoveWound(scar.Owner), Is.False);

            Assert.That(graph.TryDetachPart(part));
            Assert.That(wounds.GetWounds((part, woundable)).Count(HasScar), Is.EqualTo(1));
            Assert.That(graph.TryAttachPart(torso, part));
            Assert.That(wounds.GetWounds((part, woundable)).Count(HasScar), Is.EqualTo(1));

            entities.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(wounds.GetWounds((part, woundable)), Is.Empty);
        });
    }

    private static bool HasScar(Entity<WoundComponent> wound) => wound.Comp.State == WoundState.Scarred;
}

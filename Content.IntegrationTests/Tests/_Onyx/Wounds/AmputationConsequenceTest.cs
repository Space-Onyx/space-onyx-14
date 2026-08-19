using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(AmputationSystem))]
public sealed class AmputationConsequenceTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: transplantCompatibility
  id: AmputationConsequenceTestProfile
  provides: [ AmputationConsequenceTest ]
  accepts: [ AmputationConsequenceTest ]

- type: woundableProfile
  id: AmputationConsequenceTestWoundableProfile
  amputationThresholds:
    Head:
      Slash: 70

- type: entity
  id: AmputationConsequenceTestBody
  parent: InventoryBase
  components:
  - type: Body
  - type: Damageable
  - type: WoundHost
  - type: InitialBody
    organs:
      Chest: AmputationConsequenceTestTorso
      Head: AmputationConsequenceTestHead

- type: entity
  id: AmputationConsequenceTestTorso
  components:
  - type: BodyPart
    partType: Chest
  - type: Woundable
    profile: AmputationConsequenceTestWoundableProfile
  - type: TransplantCompatibility
    profile: AmputationConsequenceTestProfile

- type: entity
  id: AmputationConsequenceTestHead
  components:
  - type: BodyPart
    partType: Head
  - type: Woundable
    profile: AmputationConsequenceTestWoundableProfile
  - type: TransplantCompatibility
    profile: AmputationConsequenceTestProfile

- type: entity
  id: AmputationConsequenceTestHeal
  components: [ { type: SurgeryHealAmputationConsequenceEffect } ]
";

    [Test]
    public async Task TraumaticAmputationCreatesBlockingConsequenceTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 180)));
            Assert.That(graph.BodyHasChild(body, head), Is.False);

            var wound = entities.System<WoundSystem>()
                .GetWounds((torso, entities.GetComponent<WoundableComponent>(torso)))
                .Single(w => w.Comp.Prototype == "AmputationConsequenceWound");
            Assert.That(wound.Comp.Severity, Is.EqualTo(FixedPoint2.New(35)));
            Assert.That(graph.HasAmputationConsequence(torso), Is.True);
            var damage = entities.System<DamageableSystem>()
                .GetAllDamage((torso, entities.GetComponent<DamageableComponent>(torso)));
            Assert.That(damage.DamageDict["Blunt"],
                Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(damage.DamageDict["Slash"],
                Is.EqualTo(FixedPoint2.New(20)));

            var spare = entities.SpawnEntity("AmputationConsequenceTestHead", map.GridCoords);
            Assert.That(graph.TryAttachPart(torso, spare), Is.False);
        });
    }

    [Test]
    public async Task SurgicalHealRemovesConsequenceAndUnblocksTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 180)));
            Assert.That(graph.HasAmputationConsequence(torso), Is.True);

            var heal = entities.SpawnEntity("AmputationConsequenceTestHeal", map.GridCoords);
            RaiseStep(heal, body, torso, entities);

            Assert.That(graph.HasAmputationConsequence(torso), Is.False);
            var damage = entities.System<DamageableSystem>()
                .GetAllDamage((torso, entities.GetComponent<DamageableComponent>(torso)));
            Assert.That(damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));

            var spare = entities.SpawnEntity("AmputationConsequenceTestHead", map.GridCoords);
            Assert.That(graph.TryAttachPart(torso, spare), Is.True);
        });
    }

    [Test]
    public async Task HealingDamageRemovesConsequenceAndUnblocksTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 180)));
            Assert.That(graph.HasAmputationConsequence(torso), Is.True);

            Assert.That(routing.TryApplyPartDamage(body, torso, Healing(15, 20)));
            Assert.That(graph.HasAmputationConsequence(torso), Is.False);

            var spare = entities.SpawnEntity("AmputationConsequenceTestHead", map.GridCoords);
            Assert.That(graph.TryAttachPart(torso, spare), Is.True);
        });
    }

    [Test]
    public async Task HealingPartAboveThresholdDoesNotAmputateTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var damage = entities.System<DamageableSystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;

            damage.SetDamage(head, Spec("Slash", 80));
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", -1)));

            Assert.That(graph.BodyHasChild(body, head), Is.True);
            Assert.That(damage.GetAllDamage(head).DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(79)));
        });
    }

    private static void RaiseStep(EntityUid effect, EntityUid body, EntityUid part, IEntityManager entities)
    {
        var ev = new SurgeryStepEvent(EntityUid.Invalid, body, part, []);
        entities.EventBus.RaiseLocalEvent(effect, ref ev);
    }

    private static DamageSpecifier Spec(string type, int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
    };

    private static DamageSpecifier Healing(int blunt, int slash) => new()
    {
        DamageDict =
        {
            [new ProtoId<DamageTypePrototype>("Blunt")] = -FixedPoint2.New(blunt),
            [new ProtoId<DamageTypePrototype>("Slash")] = -FixedPoint2.New(slash),
        },
    };
}

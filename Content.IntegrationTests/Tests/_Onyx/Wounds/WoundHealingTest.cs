using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Repairable;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Healing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

#pragma warning disable CS0618 // These tests intentionally verify the legacy Damageable projection maintained by WoundHealingSystem.
[TestFixture]
[TestOf(typeof(WoundHealingSystem))]
public sealed class WoundHealingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundHealingBody
  parent: [InventoryBase, MobBloodstream]
  components:
  - type: Body
  - type: Sprite
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
  - type: WoundHost
  - type: Repairable
  - type: InitialBody
    organs:
      Torso: WoundHealingTorso
      Head: WoundHealingHead

- type: entity
  id: WoundHealingTorso
  components:
  - type: BodyPart
    partType: Torso

- type: entity
  id: WoundHealingHead
  components:
  - type: BodyPart
    partType: Head

- type: entity
  id: WoundHealingItem
  components:
  - type: Healing
    damageContainers: [Biological]
    damage:
      types:
        Blunt: -10
        Slash: -10
        Piercing: -10
    bloodlossModifier: -10

- type: entity
  id: WoundHealingIncompatibleItem
  components:
  - type: Healing
    damageContainers: [StructuralInorganic]
    damage:
      types:
        Blunt: -10

- type: entity
  id: WoundHealingTargetingUser
  components:
  - type: Targeting
";

    [Test]
    public async Task HealsSelectedPartWoundAndProjectionTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundHealingBody", map.GridCoords);
            var item = entityManager.SpawnEntity("WoundHealingItem", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var healing = entityManager.System<WoundHealingSystem>();
            var wounds = entityManager.System<WoundSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var pain = entityManager.System<PainSystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 15)));
            Assert.That(pain.GetRawPain(body), Is.EqualTo(FixedPoint2.New(13.05)));
            var wound = wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head)))
                .Single(candidate => candidate.Comp.Prototype == new ProtoId<WoundPrototype>("BluntWound"));
            Assert.That(healing.TryApplyHealing(body, head, (item, entityManager.GetComponent<HealingComponent>(item)),
                body, out _, out _));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(wound.Comp.Severity, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(body).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(pain.GetRawPain(head), Is.EqualTo(FixedPoint2.New(13.05)));
            Assert.That(pain.GetRawPain(body), Is.EqualTo(FixedPoint2.New(13.05)));
        });
    }

    [Test]
    public async Task LegacySelectionBleedingIsolationAndValidationTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundHealingBody", map.GridCoords);
            var otherBody = entityManager.SpawnEntity("WoundHealingBody", map.GridCoords);
            var item = entityManager.SpawnEntity("WoundHealingItem", map.GridCoords);
            var incompatible = entityManager.SpawnEntity("WoundHealingIncompatibleItem", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var healing = entityManager.System<WoundHealingSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var damage = entityManager.System<DamageableSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var foreignPart = graph.GetBodyChildren(otherBody).First().Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 10)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Slash", 20)));
            Assert.That(healing.ResolveHealingPart(body, null,
                entityManager.GetComponent<HealingComponent>(item).Damage, ["Biological"], -10), Is.EqualTo(torso));

            Assert.That(healing.TryApplyHealing(body, null, (item, entityManager.GetComponent<HealingComponent>(item)),
                body, out _, out var stopped));
            Assert.That(stopped);
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(bleeding.GetPartRate(torso), Is.LessThan(bleeding.GetPartRate(head)));

            Assert.That(healing.TryApplyHealing(body, foreignPart,
                (item, entityManager.GetComponent<HealingComponent>(item)), body, out _, out _), Is.False);
            Assert.That(healing.TryApplyHealing(body, head,
                (incompatible, entityManager.GetComponent<HealingComponent>(incompatible)), body, out _, out _), Is.False);
        });
    }

    [Test]
    public async Task UntargetedHealingTreatsDamageAcrossAllPartsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WoundHealingBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var damage = entities.System<DamageableSystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Torso).Id;

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Blunt", 10)));
            Assert.That(routing.TryApplyDamage(body, Spec("Blunt", -10)));
            Assert.That(damage.GetAllDamage(head).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.New(5)));

            Assert.That(routing.TryApplyDistributedDamage(body, Spec("Heat", 10), TargetBodyPart.All,
                DamageDistribution.SplitEvenly));
            Assert.That(damage.GetAllDamage(head).DamageDict[new ProtoId<DamageTypePrototype>("Heat")],
                Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(damage.GetAllDamage(torso).DamageDict[new ProtoId<DamageTypePrototype>("Heat")],
                Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public async Task ExactTargetSelectionAndMissingRejectionTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WoundHealingBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var resolver = entities.System<TargetResolverSystem>();
            var torso = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;

            Assert.That(resolver.TryResolveExact(body, TargetBodyPart.Head, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(head));
            Assert.That(resolver.TryResolveExact(body, TargetBodyPart.Groin, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(torso));
            Assert.That(resolver.TryResolveExact(body, TargetBodyPart.LeftHand, out _), Is.False);
        });
    }

    [Test]
    public async Task RepairSelectionAndSnapshotValidationTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WoundHealingBody", map.GridCoords);
            var user = entities.SpawnEntity("WoundHealingTargetingUser", map.GridCoords);
            entities.GetComponent<TargetingComponent>(user).Target = TargetBodyPart.Head;
            var graph = entities.System<SharedBodySystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;

            var resolve = new ResolveRepairPartEvent(user);
            entities.EventBus.RaiseLocalEvent(body, ref resolve);
            Assert.That(resolve.Targeted, Is.True);
            Assert.That(resolve.Part, Is.EqualTo(head));

            var validate = new ValidateRepairPartEvent(head);
            entities.EventBus.RaiseLocalEvent(body, ref validate);
            Assert.That(validate.Valid, Is.True);
            Assert.That(graph.TryDetachPart(head), Is.True);
            validate = new ValidateRepairPartEvent(head);
            entities.EventBus.RaiseLocalEvent(body, ref validate);
            Assert.That(validate.Valid, Is.False);
        });
    }

    private static DamageSpecifier Spec(string type, int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
    };
}
#pragma warning restore CS0618

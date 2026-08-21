using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundFractureSystem))]
public sealed class WoundFractureTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundFractureBody
  parent: InventoryBase
  components:
  - type: Body
  - type: Sprite
  - type: Damageable
  - type: MovementSpeedModifier
  - type: WoundHost
  - type: InitialBody
    organs:
      Chest: WoundFractureTorso
      ArmLeft: WoundFractureArm
      LegLeft: WoundFractureLeg

- type: entity
  id: WoundFractureTorso
  components:
  - type: BodyPart
    partType: Chest

- type: entity
  id: WoundFractureArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Left

- type: entity
  id: WoundFractureLeg
  components:
  - type: BodyPart
    partType: Leg
    symmetry: Left

- type: entity
  id: WoundFractureArmor
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Leg]
    modifiers:
      coefficients:
        Blunt: 0.5
";

    [Test]
    public async Task GradeBoundariesAreDeterministicTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            ProtoId<FractureProfilePrototype> profileId = "OrganicFractureProfile";
            var profile = prototypes.Index(profileId);
            Assert.Multiple(() =>
            {
                Assert.That(WoundFractureSystem.GetGrade(profile, 14), Is.EqualTo(FractureGrade.None));
                Assert.That(WoundFractureSystem.GetGrade(profile, 15), Is.EqualTo(FractureGrade.Hairline));
                Assert.That(WoundFractureSystem.GetGrade(profile, 29), Is.EqualTo(FractureGrade.Hairline));
                Assert.That(WoundFractureSystem.GetGrade(profile, 30), Is.EqualTo(FractureGrade.Simple));
                Assert.That(WoundFractureSystem.GetGrade(profile, 49), Is.EqualTo(FractureGrade.Simple));
                Assert.That(WoundFractureSystem.GetGrade(profile, 50), Is.EqualTo(FractureGrade.Displaced));
                Assert.That(WoundFractureSystem.GetGrade(profile, 74), Is.EqualTo(FractureGrade.Displaced));
                Assert.That(WoundFractureSystem.GetGrade(profile, 75), Is.EqualTo(FractureGrade.Comminuted));
            });
        });
    }

    [Test]
    public async Task PostArmorHitAndTreatmentPreconditionsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFractureBody", map.GridCoords);
            var armor = entityManager.SpawnEntity("WoundFractureArmor", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var fractures = entityManager.System<WoundFractureSystem>();
            var leg = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Leg).Id;

            Assert.That(inventory.TryEquip(body, armor, "outerClothing"));
            Assert.That(routing.TryApplyPartDamage(body, leg, Spec(150)));
            var fracture = fractures.GetFracture(leg).Value;
            Assert.That(fracture.Comp1.Severity, Is.EqualTo(FixedPoint2.New(75)));
            Assert.That(fracture.Comp2.Grade, Is.EqualTo(FractureGrade.Comminuted));
            Assert.That(fractures.TryMend(fracture.Owner), Is.False);
            Assert.That(fractures.TryReduce(fracture.Owner));
            Assert.That(fractures.TryMend(fracture.Owner));
            Assert.That(fractures.GetFracture(leg), Is.Null);
        });
    }

    [Test]
    public async Task EffectsRefreshOnTreatmentHealingAndDetachTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFractureBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var fractures = entityManager.System<WoundFractureSystem>();
            var manipulation = entityManager.System<FractureEffectSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var leg = parts.Single(part => part.Component.PartType == BodyPartType.Leg).Id;
            var arm = parts.Single(part => part.Component.PartType == BodyPartType.Arm).Id;

            Assert.That(routing.TryApplyPartDamage(body, leg, Spec(75)));
            Assert.That(entityManager.GetComponent<MovementSpeedModifierComponent>(body).WalkSpeedModifier,
                Is.EqualTo(0.4f).Within(0.001f));

            Assert.That(routing.TryApplyPartDamage(body, arm, Spec(75)));
            Assert.That(manipulation.GetDurationMultiplier(body), Is.EqualTo(2f).Within(0.001f));
            Assert.That(fractures.TryMend(fractures.GetFracture(arm).Value.Owner));
            Assert.That(fractures.GetFracture(arm), Is.Null);
            Assert.That(manipulation.GetDurationMultiplier(body), Is.EqualTo(1f).Within(0.001f));

            Assert.That(graph.TryDetachPart(leg));
            Assert.That(entityManager.GetComponent<MovementSpeedModifierComponent>(body).WalkSpeedModifier,
                Is.EqualTo(1f).Within(0.001f));
        });
    }

    private static DamageSpecifier Spec(int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(amount) },
    };
}

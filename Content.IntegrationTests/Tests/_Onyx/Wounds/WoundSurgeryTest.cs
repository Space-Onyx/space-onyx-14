using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundSurgerySystem))]
public sealed class WoundSurgeryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundSurgeryTestPart
  components:
  - type: BodyPart
    partType: Arm
  - type: Woundable
- type: entity
  id: WoundSurgeryTestCondition
  components:
  - type: SurgeryHasWoundCondition
    woundPrototype: SlashWound
- type: entity
  id: WoundSurgeryTestBleedingTreatment
  components:
  - type: SurgeryClampBleedingEffect
    amount: 10
- type: entity
  id: WoundSurgeryTestReduce
  components: [ { type: SurgeryReduceFractureEffect } ]
- type: entity
  id: WoundSurgeryTestMend
  components: [ { type: SurgeryMendFractureEffect } ]
- type: entity
  id: WoundSurgeryTestBody
  parent: InventoryBase
  components:
  - type: Body
  - type: Damageable
  - type: WoundHost
  - type: InitialBody
    organs:
      Chest: WoundSurgeryTestTorso
      ArmLeft: WoundSurgeryTestArm
- type: entity
  id: WoundSurgeryTestTorso
  components:
  - type: BodyPart
    partType: Chest
- type: entity
  id: WoundSurgeryTestArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Left
";

    [Test]
    public async Task SelectedPartAndHighestSeverityTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wounds = entities.System<WoundSystem>();
            var selected = entities.SpawnEntity("WoundSurgeryTestPart", map.GridCoords);
            var otherPart = entities.SpawnEntity("WoundSurgeryTestPart", map.GridCoords);
            var emptyPart = entities.SpawnEntity("WoundSurgeryTestPart", map.GridCoords);
            var condition = entities.SpawnEntity("WoundSurgeryTestCondition", map.GridCoords);
            var bleedingTreatment = entities.SpawnEntity("WoundSurgeryTestBleedingTreatment", map.GridCoords);
            var low = wounds.CreateOrMergeWound(selected, "SlashWound", 10)!.Value;
            var high = wounds.CreateOrMergeWound(selected, "PiercingWound", 20)!.Value;
            var other = wounds.CreateOrMergeWound(otherPart, "SlashWound", 30)!.Value;

            Assert.That(IsValid(condition, selected, entities));
            Assert.That(IsValid(condition, emptyPart, entities), Is.False);
            RaiseStep(bleedingTreatment, selected, entities);
            Assert.That(entities.GetComponent<WoundBleedingComponent>(high).BleedingSeverity,
                Is.EqualTo(FixedPoint2.New(10)));
            RaiseStep(bleedingTreatment, selected, entities);
            Assert.That(entities.HasComponent<WoundBleedingComponent>(high), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<WoundComponent>(low).Severity, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(entities.GetComponent<WoundComponent>(high).Severity, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(entities.GetComponent<WoundComponent>(other).Severity, Is.EqualTo(FixedPoint2.New(30)));
            });
        });
    }

    [Test]
    public async Task FractureOrderAndStaleNoOpTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WoundSurgeryTestBody", map.GridCoords);
            var arm = entities.System<SharedBodySystem>().GetBodyChildren(body)
                .Single(part => part.Component.PartType == BodyPartType.Arm).Id;
            var fractures = entities.System<WoundFractureSystem>();
            var reduce = entities.SpawnEntity("WoundSurgeryTestReduce", map.GridCoords);
            var mend = entities.SpawnEntity("WoundSurgeryTestMend", map.GridCoords);

            Assert.That(entities.System<WoundDamageRoutingSystem>().TryApplyPartDamage(body, arm, Blunt(25)));
            var fracture = fractures.GetFracture(arm)!.Value;
            RaiseStep(mend, arm, entities);
            Assert.That(fracture.Comp2.Treatment, Is.EqualTo(FractureTreatment.None));
            RaiseStep(reduce, arm, entities);
            RaiseStep(mend, arm, entities);
            Assert.That(fractures.GetFracture(arm), Is.Null);
            RaiseStep(mend, arm, entities);
            Assert.That(fractures.GetFracture(arm), Is.Null);
        });
    }

    private static bool IsValid(EntityUid condition, EntityUid part, IEntityManager entities)
    {
        var ev = new SurgeryValidEvent(EntityUid.Invalid, part);
        entities.EventBus.RaiseLocalEvent(condition, ref ev);
        return !ev.Cancelled;
    }

    private static void RaiseStep(EntityUid effect, EntityUid part, IEntityManager entities)
    {
        var ev = new SurgeryStepEvent(EntityUid.Invalid, EntityUid.Invalid, part, []);
        entities.EventBus.RaiseLocalEvent(effect, ref ev);
    }

    private static DamageSpecifier Blunt(int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(amount) },
    };
}

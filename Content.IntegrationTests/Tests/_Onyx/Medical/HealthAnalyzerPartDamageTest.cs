using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Client.HealthAnalyzer.UI;
using Content.Server.Medical;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Medical;

[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerPartDamageTest : GameTest
{
    [TestCase(0.649f, true)]
    [TestCase(0.65f, false)]
    [TestCase(float.NaN, false)]
    public void ClassifiesDangerousBloodLevel(float level, bool expected)
    {
        Assert.That(HealthAnalyzerControl.IsDangerousBloodLevel(level), Is.EqualTo(expected));
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HealthAnalyzerPartDamageBody
  components:
  - type: Body
  - type: Damageable
  - type: SurgeryTarget
  - type: WoundHost
  - type: InitialBody
    organs:
      Chest: HealthAnalyzerPartDamageTorso
      Head: HealthAnalyzerPartDamageHead
      ArmLeft: HealthAnalyzerPartDamageLeftArm

- type: entity
  id: HealthAnalyzerPartDamagePlainBody
  components:
  - type: Damageable

- type: entity
  id: HealthAnalyzerPartDamageTorso
  components:
  - type: BodyPart
    partType: Chest

- type: entity
  id: HealthAnalyzerPartDamageHead
  components:
  - type: BodyPart
    partType: Head

- type: entity
  id: HealthAnalyzerPartDamageLeftArm
  components:
  - type: BodyPart
    partType: Arm
    symmetry: Left
";

    [Test]
    public async Task BuildsIsolatedPartSnapshotTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("HealthAnalyzerPartDamageBody", map.GridCoords);
            var plain = entities.SpawnEntity("HealthAnalyzerPartDamagePlainBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var damage = entities.System<DamageableSystem>();
            var analyzer = entities.System<HealthAnalyzerSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var arm = parts.Single(part => part.Component.PartType == BodyPartType.Arm).Id;

            damage.TryChangeDamage(torso, Spec("Heat", 5), true);
            damage.TryChangeDamage(head, Spec("Blunt", 10), true);
            damage.TryChangeDamage(arm, Spec("Slash", 15), true);
            damage.TryChangeDamage(body, Spec("Asphyxiation", 20), true);

            var snapshot = analyzer.BuildPartDamage(body)!;
            Assert.Multiple(() =>
            {
                Assert.That(snapshot[TargetBodyPart.Chest].GetTotal(), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(snapshot[TargetBodyPart.Groin], Is.EqualTo(snapshot[TargetBodyPart.Chest]));
                Assert.That(snapshot[TargetBodyPart.Head].DamageDict.Keys, Is.EquivalentTo(new[] { new ProtoId<DamageTypePrototype>("Blunt") }));
                Assert.That(snapshot[TargetBodyPart.LeftArm].DamageDict.Keys, Is.EquivalentTo(new[] { new ProtoId<DamageTypePrototype>("Slash") }));
                Assert.That(snapshot.Values.All(part => !part.DamageDict.ContainsKey("Asphyxiation")), Is.True);
                Assert.That(analyzer.BuildPartDamage(plain), Is.Null);
            });

            Assert.That(graph.TryDetachPart(arm), Is.True);
            Assert.That(analyzer.BuildPartDamage(body)!.ContainsKey(TargetBodyPart.LeftArm), Is.False);
        });
    }

    [Test]
    public async Task BuildsActiveWoundDiagnosticsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("HealthAnalyzerPartDamageBody", map.GridCoords);
            var plain = entities.SpawnEntity("HealthAnalyzerPartDamagePlainBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var wounds = entities.System<WoundSystem>();
            var fractures = entities.System<WoundFractureSystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var analyzer = entities.System<HealthAnalyzerSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var arm = parts.Single(part => part.Component.PartType == BodyPartType.Arm).Id;

            wounds.CreateOrMergeWound(head, "SlashWound", 10);
            Assert.That(routing.TryApplyPartDamage(body, arm, Spec("Blunt", 25)));
            var fracture = fractures.GetFracture(arm)!.Value;
            var scarSource = wounds.CreateOrMergeWound(torso, "BluntWound", 20)!.Value;
            Assert.That(wounds.CloseWound(scarSource));

            var snapshot = analyzer.BuildWoundDiagnostics(body)!;
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Parts[TargetBodyPart.Head].BleedingRate, Is.GreaterThan(0f));
                Assert.That(snapshot.Parts[TargetBodyPart.LeftArm].Fracture, Is.EqualTo(FractureGrade.Displaced));
                Assert.That(snapshot.Parts[TargetBodyPart.LeftArm].FractureTreatment, Is.EqualTo(FractureTreatment.None));
                Assert.That(snapshot.Parts[TargetBodyPart.Chest].ScarCount, Is.EqualTo(1));
                Assert.That(snapshot.Parts.ContainsKey(TargetBodyPart.Groin), Is.False);
                Assert.That(analyzer.BuildWoundDiagnostics(plain), Is.Null);
            });

            Assert.That(fractures.TryMend(fracture.Owner));
            Assert.That(fractures.GetFracture(arm), Is.Null);
            Assert.That(analyzer.BuildWoundDiagnostics(body)!.Parts.ContainsKey(TargetBodyPart.LeftArm), Is.False);
            Assert.That(graph.TryDetachPart(head));
            Assert.That(analyzer.BuildWoundDiagnostics(body)!.Parts.ContainsKey(TargetBodyPart.Head), Is.False);
        });
    }

    private static DamageSpecifier Spec(string type, int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
    };
}

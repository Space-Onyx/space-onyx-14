using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Medical.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Healing;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundBleedingSystem))]
public sealed class WoundBleedingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WoundBleedingBody
  parent: [InventoryBase, MobBloodstream]
  components:
  - type: Body
  - type: Sprite
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
  - type: WoundHost
  - type: InitialBody
    organs:
      Chest: WoundBleedingTorso
      Head: WoundBleedingHead

- type: entity
  id: WoundBleedingTorso
  components:
  - type: BodyPart
    partType: Chest

- type: entity
  id: WoundBleedingHead
  components:
  - type: BodyPart
    partType: Head
";

    [Test]
    public async Task ProjectsTreatsAndTracksAttachmentTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;
            var bloodstream = entityManager.GetComponent<BloodstreamComponent>(body);

            Assert.That(wounds.CreateOrMergeWound(head, "SlashWound", 15), Is.Not.Null);
            Assert.That(wounds.CreateOrMergeWound(torso, "PiercingWound", 10), Is.Not.Null);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(3f).Within(0.001f));
            Assert.That(entityManager.System<BloodstreamSystem>().TryModifyBleedAmount(body, 5f), Is.False);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(3f).Within(0.001f));

            var headWound = wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))).Single();
            Assert.That(bleeding.SetTreatment(headWound.Owner, BleedingTreatment.Bandaged));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(headWound).CurrentRate,
                Is.EqualTo(0.375f).Within(0.001f));
            Assert.That(bleeding.SetTreatment(headWound.Owner, BleedingTreatment.Bandaged));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(headWound).CurrentRate,
                Is.EqualTo(0.375f).Within(0.001f));
            Assert.That(bleeding.GetPartRate(torso), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.875f).Within(0.001f));

            Assert.That(graph.TryDetachPart(head));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(graph.TryAttachPart(torso, head));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.875f).Within(0.001f));

            Assert.That(bleeding.ModifyBodyBleeding(body, -20f));
            Assert.That(bleeding.GetPartRate(head), Is.Zero);
            Assert.That(bleeding.GetPartRate(torso), Is.Zero);
            Assert.That(bloodstream.BleedAmount, Is.Zero);

            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(bloodstream.BleedAmount, Is.Zero);
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))), Is.Empty);
            Assert.That(bleeding.ModifyBodyBleeding(body, 1f));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1f).Within(0.001f));
            Assert.That(graph.GetBodyChildren(body).SelectMany(part =>
                    wounds.GetWounds((part.Id, entityManager.GetComponent<WoundableComponent>(part.Id))))
                .Single().Comp.Prototype, Is.EqualTo(new ProtoId<WoundPrototype>("SystemicBleedingWound")));
        });
    }

    [Test]
    public async Task BandageReducesBleedingAndDamageReopensWoundTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();
            var part = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var wound = wounds.CreateOrMergeWound(part, "SlashWound", 30)!.Value;

            Assert.That(bleeding.ReduceBleeding(wound, 10));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(wound).BleedingSeverity,
                Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(wounds.GetWounds((part, entityManager.GetComponent<WoundableComponent>(part))).Count(), Is.EqualTo(1));

            Assert.That(bleeding.ReduceBleeding(wound, 20));
            Assert.That(bleeding.GetPartRate(part), Is.Zero);
            Assert.That(entityManager.HasComponent<WoundBleedingComponent>(wound), Is.False);
            Assert.That(wounds.CloseWound(wound));
            Assert.That(wounds.CreateOrMergeWound(part, "SlashWound", 5), Is.EqualTo(wound));
            Assert.That(entityManager.GetComponent<WoundComponent>(wound).State, Is.EqualTo(WoundState.Open));
            Assert.That(entityManager.HasComponent<WoundBleedingComponent>(wound), Is.True);
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(wound).BleedingSeverity,
                Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(bleeding.GetPartRate(part), Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task TourniquetStopsOnlySelectedPartTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var tourniquet = entities.SpawnEntity("Tourniquet", map.GridCoords);
            var body = entities.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var bleeding = entities.System<WoundBleedingSystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Chest).Id;

            Assert.That(entities.HasComponent<TourniquetComponent>(tourniquet), Is.True);
            Assert.That(entities.HasComponent<HealingComponent>(tourniquet), Is.False);
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 10)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Slash", 10)));
            Assert.That(entities.System<TourniquetSystem>().Apply(body, head));
            Assert.That(bleeding.GetPartRate(head), Is.Zero);
            Assert.That(bleeding.GetPartRate(torso), Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task TraumaticAmputationCreatesSevereStumpBleedingTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var head = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Chest).Id;

            var routing = entityManager.System<WoundDamageRoutingSystem>();
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 200)));
            Assert.That(graph.BodyHasChild(body, head), Is.True);
            Assert.That(entityManager.GetComponent<WoundableComponent>(head).Severable, Is.True);
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 15)));
            Assert.That(graph.BodyHasChild(body, head), Is.False);
            Assert.That(entityManager.System<WoundSystem>()
                .GetWounds((torso, entityManager.GetComponent<WoundableComponent>(torso)))
                .Any(wound => wound.Comp.Prototype == new ProtoId<WoundPrototype>("DismembermentWound")));
            Assert.That(entityManager.GetComponent<BloodstreamComponent>(body).BleedAmount, Is.GreaterThanOrEqualTo(40f));
        });
    }

    [Test]
    public async Task AutomaticClottingDeadlineTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        EntityUid light = default;
        EntityUid heavy = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled, true);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity, 0.1f);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMinSeconds, 0f);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds, 10f);

                body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
                var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();
                light = parts.Single(part => part.Component.PartType == BodyPartType.Chest).Id;
                heavy = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
                var wounds = entityManager.System<WoundSystem>();
                wounds.CreateOrMergeWound(light, new ProtoId<WoundPrototype>("SlashWound"), 1);
                wounds.CreateOrMergeWound(heavy, new ProtoId<WoundPrototype>("SlashWound"), 3);
            });

            await RunSeconds(0.15f);
            await server.WaitAssertion(() =>
            {
                var bleeding = entityManager.System<WoundBleedingSystem>();
                Assert.That(bleeding.GetPartRate(light), Is.Zero);
                Assert.That(bleeding.GetPartRate(heavy), Is.GreaterThan(0f));
            });

            await server.WaitAssertion(() =>
            {
                var graph = entityManager.System<SharedBodySystem>();
                Assert.That(graph.TryDetachPart(heavy));
            });
            await RunSeconds(0.2f);
            await server.WaitAssertion(() =>
            {
                var graph = entityManager.System<SharedBodySystem>();
                var bleeding = entityManager.System<WoundBleedingSystem>();
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);
                Assert.That(graph.TryAttachPart(light, heavy));
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);

                var wound = entityManager.System<WoundSystem>()
                    .GetWounds((heavy, entityManager.GetComponent<WoundableComponent>(heavy))).Single();
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);

                Assert.That(entityManager.System<WoundSystem>().ChangeSeverity(wound.Owner, 1));
                Assert.That(bleeding.GetPartRate(heavy), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled,
                    CCVars.WoundsBleedingAutoStopEnabled.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity,
                    CCVars.WoundsBleedingAutoStopSecondsPerSeverity.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMinSeconds,
                    CCVars.WoundsBleedingAutoStopMinSeconds.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds,
                    CCVars.WoundsBleedingAutoStopMaxSeconds.DefaultValue);
            });
        }
    }

    [Test]
    public async Task AutomaticClottingDisabledTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();
        EntityUid part = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled, false);
                var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
                part = entityManager.System<SharedBodySystem>().GetBodyChildren(body).First().Id;
                entityManager.System<WoundSystem>()
                    .CreateOrMergeWound(part, new ProtoId<WoundPrototype>("SlashWound"), 1);
            });
            await RunSeconds(1f);
            await server.WaitAssertion(() =>
                Assert.That(entityManager.System<WoundBleedingSystem>().GetPartRate(part), Is.GreaterThan(0f)));
        }
        finally
        {
            await server.WaitPost(() => configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled,
                CCVars.WoundsBleedingAutoStopEnabled.DefaultValue));
        }
    }

    private static DamageSpecifier Spec(string type, int amount)
    {
        return new DamageSpecifier
        {
            DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
        };
    }
}

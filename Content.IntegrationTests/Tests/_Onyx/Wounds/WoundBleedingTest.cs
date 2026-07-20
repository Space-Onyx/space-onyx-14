using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
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
      Torso: WoundBleedingTorso
      Head: WoundBleedingHead

- type: entity
  id: WoundBleedingTorso
  components:
  - type: BodyPart
    partType: Torso

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
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var bloodstream = entityManager.GetComponent<BloodstreamComponent>(body);

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 15)));
            Assert.That(routing.TryApplyPartDamage(body, torso, Spec("Piercing", 10)));
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

            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(bloodstream.BleedAmount, Is.Zero);
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))), Is.Empty);
        });
    }

    [Test]
    public async Task BandageRemovesWeakImmediatelyAndStrongAfterFiveSecondsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        EntityUid strongPart = default;

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();
            var weakPart = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            strongPart = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var weak = wounds.CreateOrMergeWound(weakPart, "SlashWound", 14)!.Value;
            var strong = wounds.CreateOrMergeWound(strongPart, "SlashWound", 15)!.Value;

            Assert.That(bleeding.SetTreatment(weak, BleedingTreatment.Bandaged));
            Assert.That(wounds.GetWounds((weakPart, entityManager.GetComponent<WoundableComponent>(weakPart))), Is.Empty);
            Assert.That(bleeding.SetTreatment(strong, BleedingTreatment.Bandaged));
            Assert.That(wounds.GetWounds((strongPart, entityManager.GetComponent<WoundableComponent>(strongPart))).Count(),
                Is.EqualTo(1));
        });

        await RunSeconds(4.9f);
        await server.WaitAssertion(() =>
            Assert.That(entityManager.System<WoundSystem>().GetWounds(
                (strongPart, entityManager.GetComponent<WoundableComponent>(strongPart))).Count(), Is.EqualTo(1)));

        await RunSeconds(0.2f);
        await server.WaitAssertion(() =>
            Assert.That(entityManager.System<WoundSystem>().GetWounds(
                (strongPart, entityManager.GetComponent<WoundableComponent>(strongPart))), Is.Empty));
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
                light = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
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

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Onyx.Body;

[TestFixture]
public sealed class BodyConsequencesTest : GameTest
{
    [Test]
    public async Task DetachingGroinRemovesChildPartsAndInventorySlotsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("MobHuman", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var groin = graph.GetBodyChildrenOfType(body, BodyPartType.Groin).Single().Id;

            Assert.That(graph.TryDetachPart(groin), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(graph.BodyHasPartType(body, BodyPartType.Groin), Is.False);
                Assert.That(graph.BodyHasPartType(body, BodyPartType.Leg), Is.False);
                Assert.That(graph.BodyHasPartType(body, BodyPartType.Foot), Is.False);
                Assert.That(inventory.HasSlot(body, "shoes"), Is.False);
                Assert.That(inventory.HasSlot(body, "socks"), Is.False);
                Assert.That(inventory.HasSlot(body, "underwearb"), Is.False);
            });
        });
    }

    [Test]
    public async Task SocksSlotDependsOnLegsNotFeetTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("MobHuman", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();

            foreach (var foot in graph.GetBodyChildrenOfType(body, BodyPartType.Foot).ToArray())
                Assert.That(graph.TryDetachPart(foot.Id), Is.True);

            Assert.That(inventory.HasSlot(body, "shoes"), Is.False);
            Assert.That(inventory.HasSlot(body, "socks"), Is.True);
        });
    }

    [Test]
    public async Task DetachingOneLegPreventsStandingTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("MobHuman", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var leg = graph.GetBodyChildrenOfType(body, BodyPartType.Leg).First().Id;

            Assert.That(graph.TryDetachPart(leg), Is.True);
            Assert.That(entityManager.System<StandingStateSystem>().IsDown(body), Is.True);

            var attempt = new StandUpAttemptEvent();
            entityManager.EventBus.RaiseLocalEvent(body, ref attempt);
            Assert.That(attempt.Cancelled, Is.True);
        });
    }
}

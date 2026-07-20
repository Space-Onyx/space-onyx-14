using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Onyx.Clothing;

[TestFixture]
[TestOf(typeof(ClothingDirtSystem))]
public sealed class ClothingDirtTest : GameTest
{
    private const string Blood = "Blood";
    private const string Vomit = "Vomit";
    private const string PuddleSolution = "puddle";

    [Test]
    public async Task DirtCapsAndWashesTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var clothing = entityManager.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            var dirt = entityManager.System<ClothingDirtSystem>();
            var solutions = entityManager.System<SharedSolutionContainerSystem>();
            var source = new Solution();
            source.AddReagent(Blood, FixedPoint2.New(15));
            source.AddReagent(Vomit, FixedPoint2.New(5));

            Assert.That(dirt.TryDirtyClothing(clothing, source, FixedPoint2.New(20)), Is.True);
            Assert.That(solutions.TryGetSolution(clothing, ClothingDirtSystem.DefaultSolutionName, out _, out var stored), Is.True);
            Assert.That(stored!.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(stored.GetTotalPrototypeQuantity("Vomit"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(entityManager.GetComponent<ClothingDirtableComponent>(clothing).DirtColor, Is.Not.Null);

            Assert.That(dirt.TryWashClothing(clothing, new ReagentId("Water", null), FixedPoint2.New(15)), Is.True);
            Assert.That(stored.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entityManager.GetComponent<ClothingDirtableComponent>(clothing).DirtColor, Is.Null);
            Assert.That(dirt.TryWashClothing(clothing, new ReagentId("Water", null), FixedPoint2.New(1)), Is.True);

            var wearer = entityManager.SpawnEntity("MobHuman", map.GridCoords);
            var shoes = entityManager.SpawnEntity("ClothingShoesColorBlack", map.GridCoords);
            var puddle = entityManager.SpawnEntity("Puddle", map.GridCoords);
            var inventory = entityManager.System<InventorySystem>();

            Assert.That(inventory.TryEquip(wearer, shoes, "shoes"), Is.True);
            Assert.That(solutions.TryGetSolution(puddle, PuddleSolution, out var solutionEnt, out _), Is.True);
            Assert.That(solutions.TryAddReagent(solutionEnt!.Value, Blood, FixedPoint2.New(5)), Is.True);

            var transform = entityManager.System<SharedTransformSystem>();
            transform.SetLocalPosition(puddle, new Vector2(1, 0));
            transform.SetLocalPosition(wearer, new Vector2(1, 0));

            Assert.That(entityManager.GetComponent<ClothingDirtableComponent>(shoes).DirtColor, Is.Not.Null);
        });
    }
}

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Onyx.Footprints;
using Content.Shared._Onyx.Footprints;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Onyx.Footprints;

[TestFixture]
[TestOf(typeof(FootprintSystem))]
public sealed class FootprintTest : GameTest
{
    [Test]
    public async Task MovingWithDirtyFeetLeavesChemicalFootprint()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(mob, "print", out var ownerSolution, out _), Is.True);
            Assert.That(solutions.TryAddReagent(ownerSolution!.Value, "Blood", FixedPoint2.New(5)), Is.True);

            var transform = entMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(mob, new Vector2(1, 0));

            var query = entMan.EntityQueryEnumerator<FootprintComponent>();
            Assert.That(query.MoveNext(out _, out var footprint), Is.True);
            Assert.That(footprint.Footprints, Has.Count.EqualTo(1));
        });
    }
}

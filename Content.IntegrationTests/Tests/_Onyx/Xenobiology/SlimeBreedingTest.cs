using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Xenobiology.Slimes;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(SlimeBreedingSystem))]
public sealed class SlimeBreedingTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void SuccessfulPettingSetsOnlyFirstTamer()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var first = SSpawn("MobMonkey");
        var second = SSpawn("MobMonkey");
        var firstPet = new InteractionSuccessEvent(first);
        SEntMan.EventBus.RaiseLocalEvent(slime, ref firstPet);
        var secondPet = new InteractionSuccessEvent(second);
        SEntMan.EventBus.RaiseLocalEvent(slime, ref secondPet);

        Assert.That(SComp<XenobioSlimeComponent>(slime).Tamer, Is.EqualTo(first));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [RunOnSide(Side.Server)]
    public async Task MitosisCreatesRequestedOneToFourOffspring(int count)
    {
        var parent = SSpawn("MobSlimeXenobioBabyGrey");
        var domain = SComp<XenobioSlimeComponent>(parent);
        domain.MinOffspring = 1;
        domain.MaxOffspring = 4;
        domain.MutationChance = 0f;

        Assert.That(SEntMan.System<SlimeBreedingSystem>().TryMitosis((parent, domain), count), Is.True);
        await RunSeconds(0.1f);

        Assert.That(SEntMan.EntityQuery<XenobioSlimeComponent>().Count(slime => slime.Owner != parent),
            Is.EqualTo(count));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task MitosisPreservesGeneticsTamerAndStomachSolution()
    {
        var parent = SSpawn("MobSlimeXenobioBabyGrey");
        var tamer = SSpawn("MobMonkey");
        var breeding = SEntMan.System<SlimeBreedingSystem>();
        var body = SEntMan.System<SharedBodySystem>();
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        var domain = SComp<XenobioSlimeComponent>(parent);
        domain.Tamer = tamer;
        domain.MutationChance = 0f;
        domain.MinOffspring = 2;
        domain.MaxOffspring = 2;
        domain.ExtractsProduced = 3;
        domain.MitosisHunger = 111f;

        var parentStomach = body.GetBodyOrgans(parent)
            .Select(organ => organ.Id)
            .Single(organ => SEntMan.HasComponent<StomachComponent>(organ));
        var parentStomachComponent = SComp<StomachComponent>(parentStomach);
        Assert.That(solutions.ResolveSolution(parentStomach,
            StomachSystem.DefaultSolutionName,
            ref parentStomachComponent.Solution), Is.True);
        solutions.TryAddReagent(parentStomachComponent.Solution!.Value, "Water", FixedPoint2.New(10), out _);

        Assert.That(breeding.TryMitosis((parent, domain), 2), Is.True);
        await RunSeconds(0.1f);

        var children = SEntMan.EntityQuery<XenobioSlimeComponent>()
            .Where(slime => slime.Owner != parent && slime.Tamer == tamer)
            .ToList();
        Assert.That(children, Has.Count.EqualTo(2));

        var totalWater = FixedPoint2.Zero;
        foreach (var child in children)
        {
            Assert.Multiple(() =>
            {
                Assert.That(child.Breed, Is.EqualTo(domain.Breed));
                Assert.That(child.MutationChance, Is.Zero);
                Assert.That(child.MinOffspring, Is.EqualTo(2));
                Assert.That(child.MaxOffspring, Is.EqualTo(2));
                Assert.That(child.ExtractsProduced, Is.EqualTo(3));
                Assert.That(child.MitosisHunger, Is.EqualTo(111f));
            });

            var stomach = body.GetBodyOrgans(child.Owner)
                .Select(organ => organ.Id)
                .Single(organ => SEntMan.HasComponent<StomachComponent>(organ));
            var stomachComponent = SComp<StomachComponent>(stomach);
            Assert.That(solutions.ResolveSolution(stomach,
                StomachSystem.DefaultSolutionName,
                ref stomachComponent.Solution,
                out var solution), Is.True);
            totalWater += solution.GetTotalPrototypeQuantity("Water");
        }

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.Deleted(parent), Is.True);
            Assert.That(totalWater, Is.EqualTo(FixedPoint2.New(10)));
        });
    }
}

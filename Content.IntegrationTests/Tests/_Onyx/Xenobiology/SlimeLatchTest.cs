using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Xenobiology.Slimes;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(SlimeLatchSystem))]
public sealed class SlimeLatchTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestOnyxSlimeLatchTarget
  parent: InventoryBase
  components:
  - type: MobState
  - type: Body
";

    [Test]
    [RunOnSide(Side.Server)]
    public async Task LatchCreatesRelationAndUnlatchCleansIt()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var target = SSpawn("TestOnyxSlimeLatchTarget");
        var system = SEntMan.System<SlimeLatchSystem>();

        Assert.That(system.CanLatch((slime, SComp<XenobioSlimeComponent>(slime)), target), Is.True);
        system.Latch((slime, SComp<XenobioSlimeComponent>(slime)), target);

        Assert.Multiple(() =>
        {
            Assert.That(SComp<XenobioSlimeComponent>(slime).LatchedTarget, Is.EqualTo(target));
            Assert.That(SComp<BeingLatchedComponent>(target).Slime, Is.EqualTo(slime));
            Assert.That(SComp<SlimeDigestingComponent>(target).Slime, Is.EqualTo(slime));
            Assert.That(SComp<TransformComponent>(slime).ParentUid, Is.EqualTo(target));
            Assert.That(SComp<TransformComponent>(slime).LocalPosition, Is.EqualTo(Vector2.Zero));
            Assert.That(SComp<InputMoverComponent>(slime).CanMove, Is.False);
        });

        SEntMan.System<SharedTransformSystem>().SetLocalPosition(slime, Vector2.One);
        await RunSeconds(0.1f);
        Assert.Multiple(() =>
        {
            Assert.That(SComp<TransformComponent>(slime).ParentUid, Is.EqualTo(target));
            Assert.That(SComp<TransformComponent>(slime).LocalPosition, Is.EqualTo(Vector2.Zero));
        });

        system.Unlatch(slime);
        await RunSeconds(0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(SComp<XenobioSlimeComponent>(slime).LatchedTarget, Is.Null);
            Assert.That(SEntMan.HasComponent<BeingLatchedComponent>(target), Is.False);
            Assert.That(SEntMan.HasComponent<SlimeDigestingComponent>(target), Is.False);
            Assert.That(SComp<InputMoverComponent>(slime).CanMove, Is.True);
            Assert.That(SComp<TransformComponent>(slime).ParentUid, Is.Not.EqualTo(target));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task PullAttemptUnlatchesWithoutStartingPullOrLeavingMovementBlocked()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var target = SSpawn("TestOnyxSlimeLatchTarget");
        var puller = SSpawn("MobHuman");
        var system = SEntMan.System<SlimeLatchSystem>();

        system.Latch((slime, SComp<XenobioSlimeComponent>(slime)), target);
        var attempt = new PullAttemptEvent(puller, slime);
        SEntMan.EventBus.RaiseLocalEvent(slime, attempt);
        await RunSeconds(0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Cancelled, Is.True);
            Assert.That(SComp<XenobioSlimeComponent>(slime).LatchedTarget, Is.Null);
            Assert.That(SComp<InputMoverComponent>(slime).CanMove, Is.True);
            Assert.That(SComp<PullableComponent>(slime).Puller, Is.Null);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public async Task DigestionTransfersAllBloodstreamSolutionsAndActualHunger()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var target = SSpawn("MobMonkey");
        var latch = SEntMan.System<SlimeLatchSystem>();
        var body = SEntMan.System<SharedBodySystem>();
        var hungerSystem = SEntMan.System<HungerSystem>();
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        var hunger = SComp<HungerComponent>(slime);
        var bloodstream = SComp<BloodstreamComponent>(target);
        var stomach = body.GetBodyOrgans(slime)
            .Select(organ => organ.Id)
            .Single(organ => SEntMan.HasComponent<StomachComponent>(organ));
        var stomachComponent = SComp<StomachComponent>(stomach);

        Assert.Multiple(() =>
        {
            Assert.That(solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution), Is.True);
            Assert.That(solutions.ResolveSolution(target, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution), Is.True);
            Assert.That(solutions.ResolveSolution(target, bloodstream.BloodTemporarySolutionName, ref bloodstream.TemporarySolution), Is.True);
            Assert.That(solutions.ResolveSolution(stomach, StomachSystem.DefaultSolutionName, ref stomachComponent.Solution), Is.True);
        });

        var blood = bloodstream.BloodSolution!.Value;
        var metabolites = bloodstream.MetabolitesSolution!.Value;
        var temporary = bloodstream.TemporarySolution!.Value;
        var stomachSolution = stomachComponent.Solution!.Value;
        solutions.RemoveAllSolution(blood);
        solutions.RemoveAllSolution(metabolites);
        solutions.RemoveAllSolution(temporary);
        solutions.TryAddReagent(blood, "Blood", FixedPoint2.New(10), out _);
        solutions.TryAddReagent(metabolites, "Ethanol", FixedPoint2.New(10), out _);
        solutions.TryAddReagent(temporary, "Water", FixedPoint2.New(10), out _);
        hungerSystem.SetHunger(slime, 10f, hunger);

        latch.Latch((slime, SComp<XenobioSlimeComponent>(slime)), target);
        await RunSeconds(1.1f);

        Assert.Multiple(() =>
        {
            Assert.That(blood.Comp.Solution.Volume, Is.LessThan(FixedPoint2.New(10)));
            Assert.That(metabolites.Comp.Solution.GetTotalPrototypeQuantity("Ethanol"), Is.LessThan(FixedPoint2.New(10)));
            Assert.That(temporary.Comp.Solution.Volume, Is.LessThan(FixedPoint2.New(10)));
            Assert.That(metabolites.Comp.Solution.GetTotalPrototypeQuantity("XenobioSlimeToxin"), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(stomachSolution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(2.5)));
            Assert.That(hungerSystem.GetHunger(hunger), Is.GreaterThan(10f));
        });

        var bloodBeforeFullTick = blood.Comp.Solution.Volume;
        var hungerBeforeFullTick = hungerSystem.GetHunger(hunger);
        solutions.TryAddReagent(stomachSolution,
            "Water",
            stomachSolution.Comp.Solution.AvailableVolume,
            out _);
        await RunSeconds(1.1f);

        Assert.Multiple(() =>
        {
            Assert.That(blood.Comp.Solution.Volume, Is.EqualTo(bloodBeforeFullTick));
            Assert.That(hungerSystem.GetHunger(hunger), Is.LessThanOrEqualTo(hungerBeforeFullTick));
        });
    }
}

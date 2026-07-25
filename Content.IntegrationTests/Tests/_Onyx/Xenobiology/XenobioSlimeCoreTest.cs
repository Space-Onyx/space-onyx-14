using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Xenobiology.Slimes;
using Content.Shared._Onyx.Mobs.Growth;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Body;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(XenobioSlimeSystem))]
public sealed class XenobioSlimeCoreTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void GreySlimeHasValidDomainCore()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var domain = SComp<XenobioSlimeComponent>(slime);
        var growth = SComp<MobGrowthComponent>(slime);
        var organs = SComp<BodyComponent>(slime).Organs!.ContainedEntities;

        Assert.Multiple(() =>
        {
            Assert.That(domain.Breed.Id, Is.EqualTo("MobSlimeXenobioBabyGrey"));
            Assert.That(domain.BreedName.Id, Is.EqualTo("xenobio-breed-grey"));
            Assert.That(domain.Color, Is.EqualTo(Color.FromHex("#828282")));
            Assert.That(domain.ProducedExtract?.Id, Is.EqualTo("GreySlimeExtract"));
            Assert.That(domain.PotentialMutations, Has.Count.EqualTo(4));
            Assert.That(growth.CurrentStage, Is.EqualTo("baby"));
            Assert.That(organs.Count(organ => SEntMan.HasComponent<MetabolizerComponent>(organ)), Is.EqualTo(2));
            Assert.That(SProtoMan.HasIndex<EntityPrototype>(new EntProtoId("XenobioSlimeBabySpawner")), Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void WaterTouchDealsGoobEquivalentDamage()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var reactions = SEntMan.System<ReactiveSystem>();
        var damage = SEntMan.System<DamageableSystem>();

        reactions.ReactionEntity(slime,
            ReactionMethod.Touch,
            new ReagentQuantity("Water", FixedPoint2.New(1)));

        Assert.That(damage.GetAllDamage((slime, SComp<DamageableComponent>(slime))).DamageDict["Caustic"],
            Is.EqualTo(FixedPoint2.New(5)));
    }
}

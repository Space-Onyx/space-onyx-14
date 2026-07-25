using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Metabolism;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(MetabolizerComponent))]
public sealed class XenobioSlimeBodyTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void HasCoreMetabolismAndToxin()
    {
        var slime = SSpawn("MobSlimeXenobioBabyGrey");
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        var core = SComp<BodyComponent>(slime).Organs!.ContainedEntities
            .Single(organ => SComp<MetaDataComponent>(organ).EntityPrototype?.ID == "XenobioSentientSlimesCore");
        var metabolizer = SComp<MetabolizerComponent>(core);

        Assert.Multiple(() =>
        {
            Assert.That(metabolizer.MetabolizerTypes, Does.Contain(new ProtoId<MetabolizerTypePrototype>("Slime")));
            Assert.That(metabolizer.MetabolizerTypes, Does.Contain(new ProtoId<MetabolizerTypePrototype>("XenobioSlime")));
            Assert.That(solutions.TryGetSolution(core, "stomach", out _, out var stomach), Is.True);
            Assert.That(stomach!.MaxVolume.Int(), Is.EqualTo(50));
            Assert.That(solutions.TryGetSolution(slime, "bloodstream", out _, out _), Is.True);
            Assert.That(SProtoMan.HasIndex<ReagentPrototype>(new ProtoId<ReagentPrototype>("XenobioSlimeToxin")), Is.True);
        });
    }
}

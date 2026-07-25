using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(SlimeExtractSystem))]
public sealed class SlimeExtractTest : GameTest
{
    private static readonly string[] ImplementedExtracts =
    [
        "GreySlimeExtract",
        "OrangeSlimeExtract",
        "PurpleSlimeExtract",
        "BlueSlimeExtract",
        "MetalSlimeExtract",
        "YellowSlimeExtract",
        "DarkPurpleSlimeExtract",
        "DarkBlueSlimeExtract",
        "SilverSlimeExtract",
        "CeruleanSlimeExtract",
        "BluespaceSlimeExtract",
        "SepiaSlimeExtract",
        "PyriteSlimeExtract",
        "RedSlimeExtract",
        "GreenSlimeExtract",
        "PinkSlimeExtract",
        "GoldSlimeExtract",
        "OilSlimeExtract",
        "LightPinkSlimeExtract",
        "BlackSlimeExtract",
        "AdamantineSlimeExtract",
    ];

    [Test]
    [RunOnSide(Side.Server)]
    public void ExtractIsAtomicOneShotAndModifySlimeClampsGenetics()
    {
        var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var extract = SEntMan.AddComponent<SlimeExtractComponent>(uid);
        var slime = SEntMan.AddComponent<XenobioSlimeComponent>(uid);
        SEntMan.AddComponent<ReactiveComponent>(uid);
        var effects = SEntMan.System<SharedEntityEffectsSystem>();
        var use = new UseSlimeExtract
        {
            Effects =
            [
                new ModifySlime
                {
                    ExtractBonus = -10,
                    OffspringBonus = -10,
                    ChanceModifier = 10f,
                },
            ],
        };

        effects.ApplyEffect(uid, use);
        effects.ApplyEffect(uid, use);

        Assert.Multiple(() =>
        {
            Assert.That(extract.Used, Is.True);
            Assert.That(SEntMan.HasComponent<ReactiveComponent>(uid), Is.False);
            Assert.That(slime.ExtractsProduced, Is.EqualTo(1));
            Assert.That(slime.MaxOffspring, Is.EqualTo(slime.MinOffspring));
            Assert.That(slime.MutationChance, Is.EqualTo(1f));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ImplementedExtractsHaveThreeOneShotReactions()
    {
        foreach (var id in ImplementedExtracts)
        {
            var prototype = SProtoMan.Index<EntityPrototype>(id);
            Assert.That(prototype.TryComp<RefillableSolutionComponent>(out var refillable, SEntMan.ComponentFactory), Is.True, id);
            Assert.That(refillable!.Solution, Is.EqualTo("extract"), id);
            Assert.That(prototype.TryComp<ReactiveComponent>(out var reactive, SEntMan.ComponentFactory), Is.True, id);
            Assert.That(reactive!.Reactions, Has.Count.EqualTo(3), id);

            foreach (var reaction in reactive.Reactions!)
            {
                Assert.That(reaction.Effects, Has.Length.EqualTo(1), id);
                Assert.That(reaction.Effects[0], Is.TypeOf<UseSlimeExtract>(), id);
            }
        }
    }
}

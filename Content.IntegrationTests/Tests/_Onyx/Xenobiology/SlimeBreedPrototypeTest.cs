using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(XenobioSlimeComponent))]
public sealed class SlimeBreedPrototypeTest : GameTest
{
    private static readonly string[] BreedIds =
    [
        "MobSlimeXenobioBabyGrey",
        "MobSlimeXenobioBabyOrange",
        "MobSlimeXenobioBabyPurple",
        "MobSlimeXenobioBabyBlue",
        "MobSlimeXenobioBabyMetal",
        "MobSlimeXenobioBabyYellow",
        "MobSlimeXenobioBabyDarkPurple",
        "MobSlimeXenobioBabyDarkBlue",
        "MobSlimeXenobioBabySilver",
        "MobSlimeXenobioBabyCerulean",
        "MobSlimeXenobioBabyBluespace",
        "MobSlimeXenobioBabySepia",
        "MobSlimeXenobioBabyPyrite",
        "MobSlimeXenobioBabyRed",
        "MobSlimeXenobioBabyGreen",
        "MobSlimeXenobioBabyPink",
        "MobSlimeXenobioBabyGold",
        "MobSlimeXenobioBabyOil",
        "MobSlimeXenobioBabyLightPink",
        "MobSlimeXenobioBabyBlack",
        "MobSlimeXenobioBabyAdamantine",
    ];

    [Test]
    [RunOnSide(Side.Server)]
    public void AllBreedsSpawnWithValidExtractsAndReachableMutationGraph()
    {
        var graph = new Dictionary<string, HashSet<string>>();
        foreach (var id in BreedIds)
        {
            var uid = SSpawn(id);
            var slime = SComp<XenobioSlimeComponent>(uid);
            Assert.That(slime.Breed.Id, Is.EqualTo(id));
            Assert.That(slime.ProducedExtract, Is.Not.Null, id);
            Assert.That(SProtoMan.TryIndex<EntityPrototype>(slime.ProducedExtract!.Value, out var extract), Is.True, id);
            Assert.That(extract!.TryComp<SlimeExtractComponent>(out _, SEntMan.ComponentFactory), Is.True, id);

            graph[id] = slime.PotentialMutations.Select(mutation => mutation.Id).ToHashSet();
            foreach (var mutation in graph[id])
                Assert.That(BreedIds, Does.Contain(mutation), $"{id} mutation {mutation}");
        }

        var reached = new HashSet<string>();
        var pending = new Queue<string>();
        pending.Enqueue("MobSlimeXenobioBabyGrey");
        while (pending.TryDequeue(out var current))
        {
            if (!reached.Add(current))
                continue;
            foreach (var mutation in graph[current])
                pending.Enqueue(mutation);
        }

        Assert.That(reached, Is.EquivalentTo(BreedIds));
    }
}

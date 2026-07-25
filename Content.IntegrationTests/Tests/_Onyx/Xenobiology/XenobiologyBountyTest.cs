using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Onyx.Xenobiology.Bounties;
using Content.Shared._Onyx.Xenobiology.Bounties;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

[TestOf(typeof(XenobiologyBountySystem))]
public sealed class XenobiologyBountyTest : GameTest
{
    private static readonly ProtoId<TagPrototype> GreyExtractTag = "XenobiologyGreyExtract";

    [Test]
    [RunOnSide(Side.Server)]
    public void CatalogPoolAndNestedMixedPartialPlanAreExact()
    {
        var prototypes = SProtoMan.EnumeratePrototypes<XenobiologyBountyPrototype>().ToArray();
        Assert.That(prototypes, Has.Length.EqualTo(27));
        Assert.That(prototypes.All(prototype => prototype.PointsAwarded > 0), Is.True);

        var system = SEntMan.System<XenobiologyBountySystem>();
        var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var database = SEntMan.AddComponent<StationXenobiologyBountyDatabaseComponent>(station);
        system.FillDatabase((station, database));
        Assert.Multiple(() =>
        {
            Assert.That(database.Bounties, Has.Count.EqualTo(27));
            Assert.That(database.Bounties.Select(bounty => bounty.Bounty).Distinct().Count(), Is.EqualTo(27));
            Assert.That(database.Bounties.Select(bounty => bounty.Id).Distinct().Count(), Is.EqualTo(27));
            Assert.That(database.Bounties.All(bounty => bounty.Id.StartsWith("NT", StringComparison.Ordinal)), Is.True);
        });

        var containers = SEntMan.System<SharedContainerSystem>();
        var root = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var nested = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var rootContainer = containers.EnsureContainer<Container>(root, "root");
        var nestedContainer = containers.EnsureContainer<Container>(nested, "nested");
        Assert.That(containers.Insert(nested, rootContainer), Is.True);

        var grey = SSpawn("SheetSteel");
        var orange = SSpawn("OrangeSlimeExtract");
        SEntMan.AddComponent<SlimeExtractComponent>(grey);
        SEntMan.System<TagSystem>().AddTag(grey, GreyExtractTag);
        var stack = SComp<StackComponent>(grey);
        SEntMan.System<SharedStackSystem>().SetCount((grey, stack), 5);
        Assert.That(containers.Insert(grey, nestedContainer), Is.True);
        Assert.That(containers.Insert(orange, nestedContainer), Is.True);

        XenobiologyBountyItemEntry[] entries =
        [
            new()
            {
                Amount = 3,
                Name = "bounty-item-grey-extract",
                Whitelist = new EntityWhitelist { Tags = ["XenobiologyGreyExtract"] },
            },
            new()
            {
                Name = "bounty-item-orange-extract",
                Whitelist = new EntityWhitelist { Components = ["SlimeExtract"] },
                Blacklist = new EntityWhitelist { Tags = ["XenobiologyGreyExtract"] },
            },
        ];

        Assert.That(system.TryBuildConsumptionPlan(root, entries, out var plan), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(plan[grey], Is.EqualTo(3));
            Assert.That(plan[orange], Is.EqualTo(1));
            Assert.That(stack.Count, Is.EqualTo(5), "planning must not consume before fulfillment commits");
        });
    }
}

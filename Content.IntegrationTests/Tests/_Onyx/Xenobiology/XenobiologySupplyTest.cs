using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Storage.Components;
using Content.Server.Store.Conditions;
using Content.Shared.Access.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers;
using Content.Shared.EntityTable;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

public sealed class XenobiologySupplyTest : GameTest
{
    private static readonly EntProtoId SlimeCubePrototype = "SlimeCube";
    private static readonly EntProtoId RemovedGreySlimeCrate = "CrateNPCGreySlime";
    private static readonly ProtoId<ListingPrototype> UplinkBoxXenobioListing = "UplinkBoxXenobio";

    private static readonly Dictionary<string, string> CargoIcons = new()
    {
        ["LivestockXenobioSlimes"] = "CargoIconXenobioSlimeGrey",
        ["CrateNPCOrangeSlime"] = "CargoIconXenobioSlimeOrange",
        ["CrateNPCPurpleSlime"] = "CargoIconXenobioSlimePurple",
        ["CrateNPCBlueSlime"] = "CargoIconXenobioSlimeBlue",
        ["CrateNPCMetalSlime"] = "CargoIconXenobioSlimeMetal",
    };

    private static readonly string[] EntityIds =
    [
        "CrateNPCXenobioSlime",
        "CrateNPCBlueSlime",
        "CrateNPCPurpleSlime",
        "CrateNPCOrangeSlime",
        "CrateNPCMetalSlime",
        "SlimeCubeBox",
        "SlimeCubeBoxSyndie",
        "SlimeCubeWrapped",
        "SlimeCubeWrappedSyndie",
        "SlimeCube",
    ];

    [Test]
    [RunOnSide(Side.Server)]
    public void SupplyRecoveryAndUplinkMatchSource()
    {
        foreach (var id in EntityIds)
        {
            Assert.That(SProtoMan.TryIndex<EntityPrototype>(id, out _), Is.True, id);
            Assert.That(() => SSpawn(id), Throws.Nothing, id);
        }

        AssertCargo("LivestockXenobioSlimes", "CrateNPCXenobioSlime", 1800);
        AssertCargo("CrateNPCBlueSlime", "CrateNPCBlueSlime", 1500);
        AssertCargo("CrateNPCPurpleSlime", "CrateNPCPurpleSlime", 1500);
        AssertCargo("CrateNPCOrangeSlime", "CrateNPCOrangeSlime", 1500);
        AssertCargo("CrateNPCMetalSlime", "CrateNPCMetalSlime", 1500);
        Assert.That(SProtoMan.HasIndex<EntityPrototype>(RemovedGreySlimeCrate), Is.False);

        AssertFill("CrateNPCXenobioSlime", "MobSlimeXenobioBabyGrey", 2, "entity_storage");
        AssertFill("CrateNPCBlueSlime", "MobSlimeXenobioBabyBlue", 1, "entity_storage");
        AssertFill("CrateNPCPurpleSlime", "MobSlimeXenobioBabyPurple", 1, "entity_storage");
        AssertFill("CrateNPCOrangeSlime", "MobSlimeXenobioBabyOrange", 1, "entity_storage");
        AssertFill("CrateNPCMetalSlime", "MobSlimeXenobioBabyMetal", 1, "entity_storage");
        AssertFill("SlimeCubeBox", "SlimeCubeWrapped", 9, "storagebase");
        AssertFill("SlimeCubeBoxSyndie", "SlimeCubeWrappedSyndie", 9, "storagebase");

        AssertWrappedCube("SlimeCubeWrapped");
        AssertWrappedCube("SlimeCubeWrappedSyndie");

        var cube = SProtoMan.Index(SlimeCubePrototype);
        Assert.That(cube.TryComp<RehydratableComponent>(out var rehydratable, SEntMan.ComponentFactory), Is.True);
        Assert.That(rehydratable!.PossibleSpawns.Select(id => id.Id), Is.EquivalentTo([
            "MobSlimeXenobioBabyGrey",
            "MobSlimeXenobioBabyPurple",
            "MobSlimeXenobioBabyBlue",
        ]));

        var listing = SProtoMan.Index(UplinkBoxXenobioListing);
        Assert.Multiple(() =>
        {
            Assert.That(listing.ProductEntity?.Id, Is.EqualTo("SlimeCubeBoxSyndie"));
            Assert.That(listing.Cost.Single().Key.Id, Is.EqualTo("Telecrystal"));
            Assert.That(listing.Cost.Single().Value.Int(), Is.EqualTo(250));
            Assert.That(listing.Categories.Select(id => id.Id), Is.EquivalentTo(["UplinkDisruption"]));
            Assert.That(listing.Conditions, Has.Count.EqualTo(1));
        });

        var condition = listing.Conditions!.Single() as StoreWhitelistCondition;
        Assert.That(condition, Is.Not.Null);
        Assert.That(condition!.Whitelist!.Tags!.Select(id => id.Id), Is.EquivalentTo(["NukeOpsUplink"]));
    }

    private void AssertCargo(string id, string product, int cost)
    {
        var cargo = SProtoMan.Index<CargoProductPrototype>(id);
        Assert.Multiple(() =>
        {
            Assert.That(cargo.Product.Id, Is.EqualTo(product));
            Assert.That(cargo.Cost, Is.EqualTo(cost));
            Assert.That(cargo.Category, Is.EqualTo("cargoproduct-category-name-science"));
            Assert.That(cargo.Group.Id, Is.EqualTo("market"));
            Assert.That(cargo.Icon, Is.TypeOf<SpriteSpecifier.EntityPrototype>());
            Assert.That(((SpriteSpecifier.EntityPrototype) cargo.Icon).EntityPrototypeId, Is.EqualTo(CargoIcons[id]));
        });
    }

    private void AssertFill(string prototypeId, string contentId, int count, string containerId)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<EntityTableContainerFillComponent>(out var fill, SEntMan.ComponentFactory), Is.True);
        Assert.That(fill!.Containers.Keys, Is.EquivalentTo([containerId]));

        var contents = SEntMan.System<EntityTableSystem>().GetSpawns(fill.Containers[containerId]).Select(id => id.Id);
        Assert.That(contents, Is.EquivalentTo(Enumerable.Repeat(contentId, count)), prototypeId);
        Assert.That(prototype.HasComp<AccessReaderComponent>(SEntMan.ComponentFactory), Is.False, prototypeId);
    }

    private void AssertWrappedCube(string prototypeId)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<SpawnItemsOnUseComponent>(out var unwrap, SEntMan.ComponentFactory), Is.True);
        Assert.That(unwrap!.Uses, Is.EqualTo(1));
        Assert.That(unwrap.Items, Has.Count.EqualTo(1));
        Assert.That(unwrap.Items[0].PrototypeId?.Id, Is.EqualTo("SlimeCube"));
        Assert.That(unwrap.Items[0].Amount, Is.EqualTo(1));
    }
}

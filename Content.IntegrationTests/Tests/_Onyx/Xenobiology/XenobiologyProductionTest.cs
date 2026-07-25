using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.EntityTable;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

public sealed class XenobiologyProductionTest : GameTest
{
    private static readonly ProtoId<TechnologyPrototype> XenobiologyTechnology = "Xenobiology";
    private static readonly ProtoId<TechnologyPrototype> XenobagHoldingTechnology = "XenobagHolding";
    private static readonly ProtoId<TechnologyPrototype> XenoCompatibilityTechnology = "XenoCompatibility";
    private static readonly ProtoId<LatheRecipePackPrototype> XenobioPack = "XenobioPack";
    private static readonly ProtoId<LatheRecipePackPrototype> ScienceBoardsXenobiologyPack = "ScienceBoardsXenobiology";
    private static readonly ProtoId<LatheRecipePackPrototype> XenobagHoldingPack = "XenobagHoldingPack";
    private static readonly EntProtoId ProtolathePrototype = "Protolathe";
    private static readonly EntProtoId CircuitImprinterPrototype = "CircuitImprinter";
    private static readonly ProtoId<EntityTablePrototype> LockerFillTable = "FillLockerScienceXenobiology";

    private static readonly string[] EntityIds =
    [
        "SlimeGrinder",
        "SlimeGrinderMachineCircuitboard",
        "SlimeScannerXenobio",
        "ComputerScienceXenobiologyBounty",
        "XenobiologyBountyComputerCircuitboard",
        "ClothingBeltChemBagXenobiology",
        "ClothingBeltChemBagXenobiologyHolding",
        "LockerScienceFilledXenobiology",
        "ClothingBackpackXenoBioTank",
        "ClothingBackpackXenoBioTankFilled",
        "WeaponXenoVacNozzle",
    ];

    private static readonly (string Recipe, string Result)[] Recipes =
    [
        ("ClothingBackpackXenoBioTank", "ClothingBackpackXenoBioTankFilled"),
        ("SlimeScannerXenobio", "SlimeScannerXenobio"),
        ("ClothingBeltChemBagXenobiologyHolding", "ClothingBeltChemBagXenobiologyHolding"),
        ("SlimeGrinderMachineCircuitboard", "SlimeGrinderMachineCircuitboard"),
        ("XenobiologyBountyComputerCircuitboard", "XenobiologyBountyComputerCircuitboard"),
    ];

    private static readonly string[] LockerContents =
    [
        "ClothingHandsGlovesLatex",
        "ClothingHeadsetScience",
        "ClothingMaskSterile",
        "ClothingOuterVestTank",
        "ClothingBeltChemBagXenobiology",
        "ClothingBackpackXenoBioTankFilled",
        "SprayBottleWater",
        "SlimeScannerXenobio",
        "BoxSyringe",
        "PlasmaChemistryVial",
        "MonkeyCubeBox",
    ];

    [Test]
    [RunOnSide(Side.Server)]
    public void PrototypesResearchRecipesPacksAndLockerAreComplete()
    {
        foreach (var id in EntityIds)
        {
            Assert.That(SProtoMan.TryIndex<EntityPrototype>(id, out _), Is.True, id);
            Assert.That(() => SSpawn(id), Throws.Nothing, id);
        }

        var xenobiology = SProtoMan.Index(XenobiologyTechnology);
        Assert.Multiple(() =>
        {
            Assert.That(xenobiology.Discipline.Id, Is.EqualTo("Experimental"));
            Assert.That(xenobiology.Tier, Is.EqualTo(1));
            Assert.That(xenobiology.Cost, Is.EqualTo(5000));
            Assert.That(xenobiology.TechnologyPrerequisites.Select(id => id.Id), Is.EquivalentTo(["BasicXenoArcheology"]));
            Assert.That(xenobiology.RecipeUnlocks.Select(id => id.Id), Is.EquivalentTo([
                "SlimeScannerXenobio",
                "XenobiologyBountyComputerCircuitboard",
                "SlimeGrinderMachineCircuitboard",
                "ClothingBackpackXenoBioTank",
            ]));
        });

        var holding = SProtoMan.Index(XenobagHoldingTechnology);
        Assert.Multiple(() =>
        {
            Assert.That(holding.Discipline.Id, Is.EqualTo("Experimental"));
            Assert.That(holding.Tier, Is.EqualTo(2));
            Assert.That(holding.Cost, Is.EqualTo(10000));
            Assert.That(holding.TechnologyPrerequisites.Select(id => id.Id), Is.EquivalentTo(["Xenobiology"]));
            Assert.That(holding.RecipeUnlocks.Select(id => id.Id), Is.EquivalentTo(["ClothingBeltChemBagXenobiologyHolding"]));
            Assert.That(SProtoMan.HasIndex(XenoCompatibilityTechnology), Is.False);
        });

        foreach (var (recipeId, result) in Recipes)
            Assert.That(SProtoMan.Index<LatheRecipePrototype>(recipeId).Result?.Id, Is.EqualTo(result), recipeId);

        Assert.That(SProtoMan.Index(XenobioPack).Recipes.Select(id => id.Id),
            Is.EquivalentTo(["ClothingBackpackXenoBioTank", "SlimeScannerXenobio"]));
        Assert.That(SProtoMan.Index(ScienceBoardsXenobiologyPack).Recipes.Select(id => id.Id),
            Is.EquivalentTo(["SlimeGrinderMachineCircuitboard", "XenobiologyBountyComputerCircuitboard"]));
        Assert.That(SProtoMan.Index(XenobagHoldingPack).Recipes.Select(id => id.Id),
            Is.EquivalentTo(["ClothingBeltChemBagXenobiologyHolding"]));

        var protolathe = SProtoMan.Index(ProtolathePrototype);
        Assert.That(protolathe.TryComp<LatheComponent>(out var protolatheLathe, SEntMan.ComponentFactory), Is.True);
        Assert.That(protolatheLathe!.DynamicPacks.Select(id => id.Id), Does.Contain("XenobioPack"));
        Assert.That(protolatheLathe.DynamicPacks.Select(id => id.Id), Does.Contain("XenobagHoldingPack"));

        var imprinter = SProtoMan.Index(CircuitImprinterPrototype);
        Assert.That(imprinter.TryComp<LatheComponent>(out var imprinterLathe, SEntMan.ComponentFactory), Is.True);
        Assert.That(imprinterLathe!.DynamicPacks.Select(id => id.Id), Does.Contain("ScienceBoardsXenobiology"));

        var table = SProtoMan.Index(LockerFillTable);
        var contents = SEntMan.System<EntityTableSystem>().GetSpawns(table).Select(id => id.Id);
        Assert.That(contents, Is.EquivalentTo(LockerContents));
    }
}

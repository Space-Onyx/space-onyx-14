using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

/// <summary>
/// Static source regressions for branches that cannot be exercised without starting the test host.
/// These assertions do not claim behavioral runtime coverage.
/// </summary>
[TestFixture]
public sealed class XenobiologySourceDefectStaticTest
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex EscapeTextRegex = new("FormattedMessage.EscapeText", RegexOptions.Compiled);
    private static readonly Regex BreedKeyRegex = new(@"(?m)^xenobio-breed-[a-z-]+\s*=", RegexOptions.Compiled);
    private static readonly Regex DisableScreenTextureRegex = new("GetScreenTexture = false", RegexOptions.Compiled);
    private static readonly Regex DisableShaderEventRegex = new("RaiseShaderEvent = false", RegexOptions.Compiled);

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Content.IntegrationTests")))
            directory = directory.Parent;

        Assert.That(directory, Is.Not.Null, "Unable to locate the repository root");
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Compact(string source) => WhitespaceRegex.Replace(source, " ");

    [Test]
    public void GrinderQueueIsNotMutatedDuringEnumeration()
    {
        var source = ReadSource("Content.Server/_Onyx/Xenobiology/Machines/SlimeGrinderSystem.cs");
        var loop = source.IndexOf("foreach (var (prototype, amount) in grinder.YieldQueue)", StringComparison.Ordinal);
        var clear = source.IndexOf("grinder.YieldQueue.Clear();", StringComparison.Ordinal);
        Assert.That(loop, Is.GreaterThanOrEqualTo(0));
        Assert.That(clear, Is.GreaterThan(loop), "queue mutation must happen after enumeration");
    }

    [Test]
    public void GrinderMachinesUpdateIndependently()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Machines/SlimeGrinderSystem.cs"));
        Assert.That(source, Does.Contain("if (grinder.ProcessingTimer > 0f) continue;"));
    }

    [Test]
    public void MitosisFailedSpawnCannotDivideByZeroOrDeleteParent()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/SlimeBreedingSystem.cs"));
        Assert.That(source, Does.Contain("if (children.Count == 0) return false; DistributeStomachs(parent, children);"));
        Assert.That(source, Does.Not.Contain("/ count"));
    }

    [Test]
    public void MitosisRejectsInvalidOffspringRangesAndSupportsOneToFour()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/SlimeBreedingSystem.cs"));
        Assert.That(source, Does.Contain("parent.Comp.MinOffspring <= 0 || parent.Comp.MaxOffspring < parent.Comp.MinOffspring"));
        Assert.That(source, Does.Contain("if (count < parent.Comp.MinOffspring || count > parent.Comp.MaxOffspring) return false;"));
        var test = ReadSource("Content.IntegrationTests/Tests/_Onyx/Xenobiology/SlimeBreedingTest.cs");
        foreach (var count in Enumerable.Range(1, 4))
            Assert.That(test, Does.Contain($"[TestCase({count})]"));
    }

    [Test]
    public void LatchTransfersBloodMetabolitesAndTemporarySolutionsSeparately()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/SlimeLatchSystem.cs"));
        Assert.That(source, Does.Contain("bloodstream.BloodSolutionName, ref bloodstream.BloodSolution"));
        Assert.That(source, Does.Contain("bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution"));
        Assert.That(source, Does.Contain("bloodstream.BloodTemporarySolutionName, ref bloodstream.TemporarySolution"));
        Assert.That(source, Does.Contain("_solutions.TryAddReagent(blood, target.Comp.ToxinReagent, target.Comp.ToxinUnits, out _);"));
        Assert.That(source, Does.Contain("AddSource(temporary, target.Comp.ToxinReagent, sources);"));
    }

    [Test]
    public void XenovacCapacityUsesGreaterThanOrEqualCheck()
    {
        var source = ReadSource("Content.Server/_Onyx/Xenobiology/Equipment/XenovacSystem.cs");
        Assert.That(source, Does.Contain("storage.Count >= tank.Comp.Capacity"));
    }

    [Test]
    public void XenovacMissingTankIsHandledBeforeSuctionOrRelease()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Equipment/XenovacSystem.cs"));
        Assert.That(source, Does.Contain("var tank = ResolveTank(ent, args.User); if (tank == null)"));
        Assert.That(source, Does.Not.Contain("&& tank == null"));
    }

    [Test]
    public void XenovacDeletionAndDestructionReleaseEntitiesAndRestoreHtnState()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Equipment/XenovacSystem.cs"));
        Assert.That(source, Does.Contain("SubscribeLocalEvent<XenovacTankComponent, DestructionEventArgs>(OnDestroyed);"));
        Assert.That(source, Does.Contain("SubscribeLocalEvent<XenovacTankComponent, EntityTerminatingEvent>(OnTankTerminating);"));
        Assert.That(source, Does.Contain("SubscribeLocalEvent<XenovacTankComponent, EntRemovedFromContainerMessage>(OnRemoved);"));
        Assert.That(source, Does.Contain("var released = _containers.EmptyContainer(storage);"));
        Assert.That(source, Does.Contain("_htn.SetHTNEnabled((uid, htn), true, 2f); RemCompDeferred<XenovacCapturedComponent>(uid);"));
    }

    [Test]
    public void ScannerLocalizesMutationBreedsAndReagentsWithSafeMarkupSeparators()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Equipment/SlimeScannerSystem.cs"));
        Assert.That(source, Does.Contain("mutationNames.Add((Loc.GetString(mutationSlime.BreedName), mutationSlime.Color.ToHex()));"));
        Assert.That(source, Does.Contain("names.Add((prototype.LocalizedName, prototype.SubstanceColor.ToHex()));"));
        Assert.That(source, Does.Contain("string.Join(\", \", mutationNames.Select"));
        Assert.That(source, Does.Contain("string.Join(\"; \", reactions)"));
        Assert.That(source, Does.Contain("extract.Comp.Used"));
        Assert.That(EscapeTextRegex.Matches(source).Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void SlimeAiAndLifecycleKeepSourceGameplay()
    {
        var prototype = Compact(ReadSource("Resources/Prototypes/_Onyx/Entities/Mobs/NPCs/Slimes/slimes.yml"));
        Assert.That(prototype, Does.Contain("factions: - SimpleHostile"));
        Assert.That(prototype, Does.Not.Contain("CorpseEater"));
        Assert.That(prototype, Does.Not.Contain("ActionXenobioEatCorpse"));

        var domain = Compact(ReadSource("Content.Shared/_Onyx/Xenobiology/Slimes/XenobioSlimeComponent.cs"));
        var digestion = Compact(ReadSource("Content.Shared/_Onyx/Xenobiology/Slimes/SlimeLatchComponent.cs"));
        Assert.That(domain, Does.Contain("AutoGenerateComponentPause"));
        Assert.That(domain, Does.Contain("AutoPausedField] public TimeSpan NextMitosis"));
        Assert.That(digestion, Does.Contain("AutoGenerateComponentPause"));
        Assert.That(digestion, Does.Contain("AutoPausedField] public TimeSpan NextTick"));

        var breeding = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/SlimeBreedingSystem.cs"));
        Assert.That(breeding, Does.Contain("CCVars.XenobiologyBreedingInterval"));
        Assert.That(breeding, Does.Contain("CCVars.XenobiologyBreedingEnabled"));
    }

    [Test]
    public void SlimeDetailsRequireScannerAndNamesHaveNoDuplicateStagePrefix()
    {
        var prototype = Compact(ReadSource("Resources/Prototypes/_Onyx/Entities/Mobs/NPCs/Slimes/slimes.yml"));
        Assert.That(prototype, Does.Contain("namePrefix: mob-growth-stage-small"));
        Assert.That(prototype, Does.Contain("drawdepth: OverMobs"));

        var system = ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/XenobioSlimeSystem.cs");
        Assert.That(system, Does.Not.Contain("ExaminedEvent"));
        Assert.That(system, Does.Not.Contain("OnExamined"));
    }

    [Test]
    public void SlimeReleaseStunHasMatchingKnockdownVisualState()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Slimes/SlimeLatchSystem.cs"));
        Assert.That(source, Does.Contain("TryAddStunDuration(target, slime.Comp.OnReleaseStunDuration, visualized: true); _stun.TryKnockdown(target, slime.Comp.OnReleaseStunDuration, force: true);"));
    }

    [Test]
    public void ExtractWarningsAndAdvancedMutationRemainFunctional()
    {
        var extract = Compact(ReadSource("Resources/Prototypes/_Onyx/Entities/Objects/Specific/Xenobiology/Extracts/base.yml"));
        Assert.That(extract, Does.Contain("type: StatusEffects allowed: [ Jitter ]"));

        var reagents = Compact(ReadSource("Resources/Prototypes/_Onyx/Reagents/Xenobiology/jellies_toxins.yml"));
        Assert.That(reagents, Does.Contain("blacklist: - Skeleton - Ipc"));

        var visualizer = Compact(ReadSource("Content.Client/_Onyx/Xenobiology/Slimes/XenobioSlimeVisualizerSystem.cs"));
        Assert.That(DisableScreenTextureRegex.Matches(visualizer).Count, Is.EqualTo(2));
        Assert.That(DisableShaderEventRegex.Matches(visualizer).Count, Is.EqualTo(2));
    }

    [Test]
    public void ExtractRestoresTriggerSolutionWhenEffectsFail()
    {
        var source = Compact(ReadSource("Content.Shared/_Onyx/Xenobiology/Extracts/SlimeExtractSystem.cs"));
        Assert.That(source, Does.Contain("catch { if (solutionEntity is { } target && original is { } restore)"));
        Assert.That(source, Does.Contain("_solutions.RemoveAllSolution(target); _solutions.TryAddSolution(target, restore);"));
        Assert.That(source.IndexOf("entity.Comp.Used = true;", StringComparison.Ordinal),
            Is.GreaterThan(source.IndexOf("_effects.ApplyEffects", StringComparison.Ordinal)));
    }

    [Test]
    public void BountyIdentifierCollisionRetries()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        Assert.That(source, Does.Contain("for (var attempt = 0; attempt < 10_000; attempt++)"));
        Assert.That(source, Does.Contain("station.Comp.History.Any(history => history.Id == bounty.Id)) continue;"));
    }

    [Test]
    public void BountyFillUsesOnlyInactiveFiniteCatalogPool()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        Assert.That(source, Does.Contain(".Where(prototype => !active.Contains(prototype.ID)) .ToList();"));
        Assert.That(source, Does.Contain("while (station.Comp.Bounties.Count < station.Comp.MaxBounties && pool.Count > 0)"));
        Assert.That(source, Does.Contain("pool.RemoveAt(pool.Count - 1);"));
    }

    [Test]
    public void BountyPartialStackConsumesOnlyPlannedAmount()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        Assert.That(source, Does.Contain("foreach (var (entity, amount) in plan)"));
        Assert.That(source, Does.Contain("_stacks.TryUse((entity, stack), amount);"));
    }

    [Test]
    public void BountyUiStateIsCreatedAfterSorting()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        Assert.That(source, Does.Contain("private void UpdateState(EntityUid console, StationXenobiologyBountyDatabaseComponent database) { SortBounties(database); _ui.SetUiState"));
    }

    [Test]
    public void BountyUpdatesOnlyConsolesOnOwningStation()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        Assert.That(source, Does.Contain("if (_station.GetOwningStation(console) == station) UpdateState(console, database);"));
    }

    [Test]
    public void FulfilledBountiesReturnOnlyOnGlobalRefresh()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/Xenobiology/Bounties/XenobiologyBountySystem.cs"));
        var fulfill = source[source.IndexOf("private void OnFulfill", StringComparison.Ordinal)..source.IndexOf("private void OnSkip", StringComparison.Ordinal)];
        Assert.That(fulfill, Does.Not.Contain("FillDatabase("));
    }

    [Test]
    public void HoldingBagHasSingleResearchBranch()
    {
        var source = ReadSource("Content.IntegrationTests/Tests/_Onyx/Xenobiology/XenobiologyProductionTest.cs");
        Assert.That(source, Does.Contain("TechnologyPrerequisites.Select(id => id.Id), Is.EquivalentTo([\"Xenobiology\"])"));
        Assert.That(source, Does.Contain("HasIndex(XenoCompatibilityTechnology), Is.False"));
    }

    [Test]
    public void RussianBreedLocalizationCoversAllTwentyOneBreeds()
    {
        var source = ReadSource("Resources/Locale/ru-RU/_Onyx/prototypes/entities/mobs/npcs/slimes.ftl");
        var keys = BreedKeyRegex.Matches(source);
        Assert.That(keys, Has.Count.EqualTo(21));
        Assert.That(keys.Cast<Match>().Select(match => match.Value), Is.Unique);
    }

    [Test]
    public void RandomSpeciesPoolValidatesSpeciesAndEntityPrototypes()
    {
        var source = Compact(ReadSource("Content.Server/_Onyx/EntityEffects/Effects/Transform/SpeciesChangeEntityEffectSystem.cs"));
        Assert.That(source, Does.Contain("_prototypes.EnumeratePrototypes<SpeciesPrototype>()"));
        Assert.That(source, Does.Contain("_prototypes.HasIndex<EntityPrototype>(species.Prototype)"));
        Assert.That(source, Does.Contain("if (candidates.Count != 0)"));
    }
}

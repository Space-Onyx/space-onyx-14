using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Popups;
using Content.Shared._Onyx.AbstractAnalyzer;
using Content.Shared._Onyx.Botany.PlantAnalyzer;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Botany.PlantAnalyzer;

public sealed partial class PlantAnalyzerSystem : AbstractAnalyzerSystem<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>
{
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private PlantAnalyzerLocalizationHelper _localizationHelper = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerPrintMessage>(OnPrint);
    }

    public override void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(analyzer, PlantAnalyzerUiKey.Key) ||
            !ValidScanTarget(target) ||
            !TryComp<PlantAnalyzerComponent>(analyzer, out var component))
            return;

        _uiSystem.ServerSendUiMessage(analyzer,
            PlantAnalyzerUiKey.Key,
            GatherData(component, scanMode, target));
    }

    private PlantAnalyzerScannedUserMessage GatherData(PlantAnalyzerComponent analyzer,
        bool? scanMode = null,
        EntityUid? target = null)
    {
        target ??= analyzer.ScannedEntity;
        PlantAnalyzerPlantData? plantData = null;
        PlantAnalyzerTrayData? trayData = null;
        PlantAnalyzerTolerancesData? tolerancesData = null;
        PlantAnalyzerProduceData? produceData = null;

        if (TryComp<PlantHolderComponent>(target, out var holder))
        {
            if (TryComp<PlantComponent>(target, out var plant)
                && TryComp<PlantDataComponent>(target, out var speciesData))
            {
                TryComp<PlantGrowthComponent>(target, out var growth);
                TryComp<PlantAtmosphericComponent>(target, out var atmosphere);
                TryComp<PlantToxinsComponent>(target, out var toxins);
                TryComp<PlantWeedPestComponent>(target, out var pests);
                TryComp<PlantConsumeExudeGasComponent>(target, out var gases);
                TryComp<PlantChemicalsComponent>(target, out var chemicals);

                plantData = new PlantAnalyzerPlantData(speciesData.Name,
                    holder.Health,
                    plant.Endurance,
                    holder.Age,
                    plant.Lifespan,
                    holder.Dead,
                    !HasComp<PlantTraitUnviableComponent>(target),
                    holder.MutationLevel > 0f,
                    HasComp<PlantTraitKudzuComponent>(target));
                tolerancesData = new PlantAnalyzerTolerancesData(growth?.NutrientConsumption ?? 0f,
                    growth?.WaterConsumption ?? 0f,
                    atmosphere is null ? 0f : (atmosphere.LowHeatTolerance + atmosphere.HighHeatTolerance) / 2f,
                    atmosphere is null ? 0f : (atmosphere.HighHeatTolerance - atmosphere.LowHeatTolerance) / 2f,
                    0f,
                    0f,
                    toxins?.ToxinsTolerance ?? 0f,
                    atmosphere?.LowPressureTolerance ?? 0f,
                    atmosphere?.HighPressureTolerance ?? 0f,
                    pests?.PestTolerance ?? 0f,
                    pests?.WeedTolerance ?? 0f,
                    gases is null ? [] : [.. gases.ConsumeGasses.Keys]);
                produceData = new PlantAnalyzerProduceData(speciesData.ProductPrototypes.Count == 0
                        ? 0
                        : CalculateTotalYield(plant.Yield, holder.YieldMod),
                    plant.Potency,
                    chemicals is null ? [] : [.. chemicals.Chemicals.Keys.Select(id => id.Id)],
                    speciesData.ProductPrototypes,
                    gases is null ? [] : [.. gases.ExudeGasses.Keys],
                    HasComp<PlantTraitSeedlessComponent>(target));
            }

            var tray = Transform(target.Value).ParentUid;
            if (TryComp<PlantTrayComponent>(tray, out var trayComp))
            {
                trayData = new PlantAnalyzerTrayData(trayComp.WaterLevel,
                    trayComp.NutritionLevel,
                    trayComp.ToxinLevel,
                    trayComp.PestLevel,
                    trayComp.WeedLevel,
                    trayComp.SoilSolution?.Comp.Solution.Contents.Select(reagent => reagent.Reagent.Prototype).ToList());
            }
        }

        return new PlantAnalyzerScannedUserMessage(GetNetEntity(target),
            scanMode,
            plantData,
            trayData,
            tolerancesData,
            produceData,
            analyzer.PrintReadyAt);
    }

    private static int CalculateTotalYield(int yield, int yieldMod)
    {
        if (yield <= -1)
            return 0;

        return Math.Max(1, yieldMod < 0 ? yield : yield * yieldMod);
    }

    private void OnPrint(EntityUid uid, PlantAnalyzerComponent component, PlantAnalyzerPrintMessage args)
    {
        var user = args.Actor;
        if (_gameTiming.CurTime < component.PrintReadyAt)
        {
            _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), uid, user);
            return;
        }

        var printed = SpawnAtPosition(component.MachineOutput, Transform(uid).Coordinates);
        _handsSystem.PickupOrDrop(user, printed, checkActionBlocker: false);
        if (!TryComp<PaperComponent>(printed, out var paper))
        {
            Log.Error("Printed paper did not have PaperComponent.");
            return;
        }

        var data = GatherData(component);
        var missing = Loc.GetString("plant-analyzer-printout-missing");
        var seedName = data.PlantData is not null ? Loc.GetString(data.PlantData.SeedDisplayName) : null;
        (string, object)[] parameters =
        [
            ("seedName", seedName ?? missing),
            ("produce", data.ProduceData is not null ? _localizationHelper.ProduceToLocalizedStrings(data.ProduceData.Produce).Plural : missing),
            ("water", data.TolerancesData?.WaterConsumption.ToString("0.00") ?? missing),
            ("nutrients", data.TolerancesData?.NutrientConsumption.ToString("0.00") ?? missing),
            ("toxins", data.TolerancesData?.ToxinsTolerance.ToString("0.00") ?? missing),
            ("pests", data.TolerancesData?.PestTolerance.ToString("0.00") ?? missing),
            ("weeds", data.TolerancesData?.WeedTolerance.ToString("0.00") ?? missing),
            ("gasesIn", data.TolerancesData is not null ? _localizationHelper.GasesToLocalizedStrings(data.TolerancesData.ConsumeGasses) : missing),
            ("kpa", data.TolerancesData?.IdealPressure.ToString("0.00") ?? missing),
            ("kpaTolerance", data.TolerancesData?.PressureTolerance.ToString("0.00") ?? missing),
            ("temp", data.TolerancesData?.IdealHeat.ToString("0.00") ?? missing),
            ("tempTolerance", data.TolerancesData?.HeatTolerance.ToString("0.00") ?? missing),
            ("lightLevel", data.TolerancesData?.IdealLight.ToString("0.00") ?? missing),
            ("lightTolerance", data.TolerancesData?.LightTolerance.ToString("0.00") ?? missing),
            ("yield", data.ProduceData?.Yield ?? -1),
            ("potency", data.ProduceData is not null ? Loc.GetString(data.ProduceData.Potency) : missing),
            ("chemicals", data.ProduceData is not null ? _localizationHelper.ChemicalsToLocalizedStrings(data.ProduceData.Chemicals) : missing),
            ("gasesOut", data.ProduceData is not null ? _localizationHelper.GasesToLocalizedStrings(data.ProduceData.ExudeGasses) : missing),
            ("endurance", data.PlantData?.Endurance.ToString("0.00") ?? missing),
            ("lifespan", data.PlantData?.Lifespan.ToString("0.00") ?? missing),
            ("seeds", data.ProduceData is not null ? data.ProduceData.Seedless ? "no" : "yes" : "other"),
            ("viable", data.PlantData is not null ? data.PlantData.Viable ? "yes" : "no" : "other"),
            ("kudzu", data.PlantData is not null ? data.PlantData.Kudzu ? "yes" : "no" : "other"),
        ];

        _paperSystem.SetContent((printed, paper), Loc.GetString("plant-analyzer-printout", parameters));
        _labelSystem.Label(printed, seedName);
        _audioSystem.PlayPvs(component.SoundPrint,
            uid,
            AudioParams.Default.WithVariation(0.25f).WithVolume(3f).WithRolloffFactor(2.8f).WithMaxDistance(4.5f));
        component.PrintReadyAt = _gameTiming.CurTime + component.PrintCooldown;
    }

    protected override Enum GetUiKey() => PlantAnalyzerUiKey.Key;

    protected override bool ScanTargetPopupMessage(Entity<PlantAnalyzerComponent> uid,
        AfterInteractEvent args,
        [NotNullWhen(true)] out string? message)
    {
        message = null;
        return false;
    }

    protected override bool ValidScanTarget(EntityUid? target) => HasComp<PlantHolderComponent>(target);
}

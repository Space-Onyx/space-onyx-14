using Content.Shared._Onyx.SeedDna;
using Content.Shared._Onyx.SeedDna.Components;
using Content.Shared._Onyx.SeedDna.Systems;
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Chemistry.Reagent;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.SeedDna.Systems;

[UsedImplicitly]
public sealed partial class SeedDnaConsoleSystem : SharedSeedDnaConsoleSystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private BotanySystem _botany = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SeedDnaConsoleComponent, WriteToTargetSeedDataMessage>(OnWriteToTargetSeedDataMessage);
        SubscribeLocalEvent<SeedDnaConsoleComponent, ComponentStartup>(OnUpdateUserInterface);
        SubscribeLocalEvent<SeedDnaConsoleComponent, EntInsertedIntoContainerMessage>(OnUpdateUserInterface);
        SubscribeLocalEvent<SeedDnaConsoleComponent, EntRemovedFromContainerMessage>(OnUpdateUserInterface);
    }

    private void OnUpdateUserInterface(EntityUid uid, SeedDnaConsoleComponent component, EntityEventArgs args) => UpdateUserInterface(uid, component);

    private void OnWriteToTargetSeedDataMessage(EntityUid uid, SeedDnaConsoleComponent component, WriteToTargetSeedDataMessage args)
    {
        if (args.Target == TargetSeedData.Seed && component.SeedSlot.Item is { Valid: true } seed)
            RewriteSeedData(seed, args.SeedDataDto);
        else if (args.Target == TargetSeedData.DnaDisk && component.DnaDiskSlot.Item is { Valid: true } disk)
            Comp<DnaDiskComponent>(disk).SeedData = args.SeedDataDto;

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, SeedDnaConsoleComponent component)
    {
        if (!component.Initialized)
            return;

        var (seedPresent, seedName, seedData) = ProcessSeedSlot(component);
        var (diskPresent, diskName, diskData) = ProcessDiskSlot(component);
        _userInterface.SetUiState(uid, SeedDnaConsoleUiKey.Key,
            new SeedDnaConsoleBoundUserInterfaceState(seedPresent, seedName, seedData, diskPresent, diskName, diskData));
    }

    private (bool, string, SeedDataDto?) ProcessSeedSlot(SeedDnaConsoleComponent component)
    {
        return component.SeedSlot.Item is not { Valid: true } seed
            ? (false, string.Empty, null)
            : (true, MetaData(seed).EntityName, ExtractSeedData(seed));
    }

    private (bool, string, SeedDataDto?) ProcessDiskSlot(SeedDnaConsoleComponent component)
    {
        return component.DnaDiskSlot.Item is not { Valid: true } disk
            ? (false, string.Empty, null)
            : (true, MetaData(disk).EntityName, Comp<DnaDiskComponent>(disk).SeedData);
    }

    private void RewriteSeedData(EntityUid seed, SeedDataDto dto)
    {
        var seedComp = Comp<SeedComponent>(seed);
        var snapshot = EnsureSnapshot(seed, seedComp);
        if (snapshot == null)
            return;

        Apply(dto, snapshot.Value);
        seedComp.PlantData = snapshot;
        Dirty(seed, seedComp);
    }

    private EntityUid? EnsureSnapshot(EntityUid seed, SeedComponent seedComp)
    {
        if (seedComp.PlantData is { } existing && !EntityManager.IsQueuedForDeletion(existing))
            return existing;

        var source = Spawn(seedComp.PlantProtoId, doMapInit: false);
        var snapshot = _botany.ClonePlantSnapshotData(source, parent: seed);
        QueueDel(source);
        return snapshot;
    }

    private void Apply(SeedDataDto dto, EntityUid plant)
    {
        if (TryComp<PlantComponent>(plant, out var growth))
        {
            if (dto.Endurance != null) growth.Endurance = dto.Endurance.Value;
            if (dto.Yield != null) growth.Yield = dto.Yield.Value;
            if (dto.Lifespan != null) growth.Lifespan = dto.Lifespan.Value;
            if (dto.Maturation != null) growth.Maturation = dto.Maturation.Value;
            if (dto.Production != null) growth.Production = dto.Production.Value;
            if (dto.Potency != null) growth.Potency = dto.Potency.Value;
        }

        if (TryComp<PlantGrowthComponent>(plant, out var water))
        {
            if (dto.WaterConsumption != null) water.WaterConsumption = dto.WaterConsumption.Value;
            if (dto.NutrientConsumption != null) water.NutrientConsumption = dto.NutrientConsumption.Value;
        }

        if (TryComp<PlantAtmosphericComponent>(plant, out var atmosphere))
        {
            var ideal = dto.IdealHeat;
            var tolerance = dto.HeatTolerance;
            if (ideal != null || tolerance != null)
            {
                var center = ideal ?? (atmosphere.LowHeatTolerance + atmosphere.HighHeatTolerance) / 2f;
                var halfRange = tolerance ?? (atmosphere.HighHeatTolerance - atmosphere.LowHeatTolerance) / 2f;
                atmosphere.LowHeatTolerance = center - halfRange;
                atmosphere.HighHeatTolerance = center + halfRange;
            }
            if (dto.LowPressureTolerance != null) atmosphere.LowPressureTolerance = dto.LowPressureTolerance.Value;
            if (dto.HighPressureTolerance != null) atmosphere.HighPressureTolerance = dto.HighPressureTolerance.Value;
        }

        if (TryComp<PlantToxinsComponent>(plant, out var toxins) && dto.ToxinsTolerance != null)
            toxins.ToxinsTolerance = dto.ToxinsTolerance.Value;
        if (TryComp<PlantWeedPestComponent>(plant, out var pests))
        {
            if (dto.PestTolerance != null) pests.PestTolerance = dto.PestTolerance.Value;
            if (dto.WeedTolerance != null) pests.WeedTolerance = dto.WeedTolerance.Value;
        }
        if (TryComp<PlantConsumeExudeGasComponent>(plant, out var gases))
        {
            if (dto.ConsumeGasses != null) gases.ConsumeGasses = dto.ConsumeGasses;
            if (dto.ExudeGasses != null) gases.ExudeGasses = dto.ExudeGasses;
        }
        if (TryComp<PlantHarvestComponent>(plant, out var harvest) && dto.HarvestRepeat != null)
            harvest.HarvestRepeat = (HarvestType)(byte)dto.HarvestRepeat.Value;
        if (TryComp<PlantChemicalsComponent>(plant, out var chemicals) && dto.Chemicals != null)
        {
            chemicals.Chemicals.Clear();
            foreach (var (id, value) in dto.Chemicals)
                chemicals.Chemicals[new ProtoId<ReagentPrototype>(id)] = new PlantChemQuantity
                {
                    Min = value.Min, Max = value.Max, PotencyDivisor = value.PotencyDivisor, Inherent = value.Inherent
                };
        }

        SetTrait<PlantTraitSeedlessComponent>(plant, dto.Seedless);
        SetTrait<PlantTraitUnviableComponent>(plant, dto.Viable == false);
        SetTrait<PlantTraitLigneousComponent>(plant, dto.Ligneous);
        SetTrait<PlantTraitScreamComponent>(plant, dto.CanScream);
    }

    private void SetTrait<T>(EntityUid plant, bool? enabled) where T : PlantTraitsComponent, new()
    {
        if (enabled == null)
            return;
        if (enabled.Value)
            EnsureComp<T>(plant);
        else
            RemComp<T>(plant);
    }

    private SeedDataDto? ExtractSeedData(EntityUid seed)
    {
        var comp = Comp<SeedComponent>(seed);
        if (!_botany.TryGetPlantComponent<PlantComponent>(comp.PlantData, comp.PlantProtoId, out var plant))
            return null;

        _botany.TryGetPlantComponent<PlantConsumeExudeGasComponent>(comp.PlantData, comp.PlantProtoId, out var gases);
        _botany.TryGetPlantComponent<PlantGrowthComponent>(comp.PlantData, comp.PlantProtoId, out var growth);

        var result = new SeedDataDto
        {
            Chemicals = null,
            ConsumeGasses = gases?.ConsumeGasses,
            ExudeGasses = gases?.ExudeGasses,
            NutrientConsumption = growth?.NutrientConsumption,
            WaterConsumption = growth?.WaterConsumption,
            Endurance = plant.Endurance, Yield = plant.Yield, Lifespan = plant.Lifespan,
            Maturation = plant.Maturation, Production = plant.Production, Potency = plant.Potency,
            Seedless = _botany.TryGetPlantComponent<PlantTraitSeedlessComponent>(comp.PlantData, comp.PlantProtoId, out _),
            Viable = !_botany.TryGetPlantComponent<PlantTraitUnviableComponent>(comp.PlantData, comp.PlantProtoId, out _),
            Ligneous = _botany.TryGetPlantComponent<PlantTraitLigneousComponent>(comp.PlantData, comp.PlantProtoId, out _),
            CanScream = _botany.TryGetPlantComponent<PlantTraitScreamComponent>(comp.PlantData, comp.PlantProtoId, out _),
        };

        if (_botany.TryGetPlantComponent<PlantAtmosphericComponent>(comp.PlantData, comp.PlantProtoId, out var atmosphere))
        {
            result.IdealHeat = (atmosphere.LowHeatTolerance + atmosphere.HighHeatTolerance) / 2f;
            result.HeatTolerance = (atmosphere.HighHeatTolerance - atmosphere.LowHeatTolerance) / 2f;
            result.LowPressureTolerance = atmosphere.LowPressureTolerance;
            result.HighPressureTolerance = atmosphere.HighPressureTolerance;
        }
        if (_botany.TryGetPlantComponent<PlantToxinsComponent>(comp.PlantData, comp.PlantProtoId, out var toxins)) result.ToxinsTolerance = toxins.ToxinsTolerance;
        if (_botany.TryGetPlantComponent<PlantWeedPestComponent>(comp.PlantData, comp.PlantProtoId, out var pests)) { result.PestTolerance = pests.PestTolerance; result.WeedTolerance = pests.WeedTolerance; }
        if (_botany.TryGetPlantComponent<PlantHarvestComponent>(comp.PlantData, comp.PlantProtoId, out var harvest)) result.HarvestRepeat = (SharedHarvestTypeDto)(byte)harvest.HarvestRepeat;
        if (_botany.TryGetPlantComponent<PlantChemicalsComponent>(comp.PlantData, comp.PlantProtoId, out var chemicals))
        {
            result.Chemicals = new Dictionary<string, SeedChemQuantityDto>();
            foreach (var (id, value) in chemicals.Chemicals)
                result.Chemicals[id.Id] = new SeedChemQuantityDto { Min = value.Min, Max = value.Max, PotencyDivisor = value.PotencyDivisor, Inherent = value.Inherent };
        }
        return result;
    }
}

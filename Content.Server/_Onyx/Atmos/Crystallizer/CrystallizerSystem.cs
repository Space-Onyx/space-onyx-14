using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Atmos.Crystallizer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Atmos.Crystallizer;

public sealed partial class CrystallizerSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private const float MinProgressAmount = 3f;
    private const float MinDeviationRate = 0.9f;
    private const float MaxDeviationRate = 1.1f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CrystallizerComponent, BeforeActivatableUIOpenEvent>(OnBeforeOpened);
        SubscribeLocalEvent<CrystallizerComponent, CrystallizerToggleMessage>(OnToggle);
        SubscribeLocalEvent<CrystallizerComponent, CrystallizerSelectRecipeMessage>(OnRecipeSelected);
        SubscribeLocalEvent<CrystallizerComponent, CrystallizerSetGasInputMessage>(OnGasInputSet);
        SubscribeLocalEvent<CrystallizerComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    private void OnBeforeOpened(Entity<CrystallizerComponent> ent, ref BeforeActivatableUIOpenEvent args) => UpdateUi(ent);

    private void OnToggle(Entity<CrystallizerComponent> ent, ref CrystallizerToggleMessage args)
    {
        _power.TogglePower(ent);
        UpdateUi(ent);
    }

    private void OnRecipeSelected(Entity<CrystallizerComponent> ent, ref CrystallizerSelectRecipeMessage args)
    {
        if (args.RecipeId == ent.Comp.SelectedRecipeId)
            return;

        if (GetPipeMixture(ent, ent.Comp.InletName, out var inlet))
        {
            _atmos.Merge(inlet, ent.Comp.CrystallizerGasMixture);
            ent.Comp.CrystallizerGasMixture = new();
        }

        ent.Comp.SelectedRecipeId = args.RecipeId;
        ent.Comp.ProgressBar = 0f;
        ent.Comp.QualityLoss = 0f;
        ent.Comp.TotalRecipeMoles = 0f;
        if (GetRecipe(ent.Comp) is { } recipe)
        {
            foreach (var moles in recipe.MinimumRequirements)
                ent.Comp.TotalRecipeMoles += moles;
        }
        UpdateUi(ent);
    }

    private void OnGasInputSet(Entity<CrystallizerComponent> ent, ref CrystallizerSetGasInputMessage args)
    {
        ent.Comp.GasInput = Math.Max(args.GasInput, 0f);
        UpdateUi(ent);
    }

    private void OnAtmosUpdate(Entity<CrystallizerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!_power.IsPowered(ent) || !HasComp<ApcPowerReceiverComponent>(ent))
            return;

        if (GetRecipe(ent.Comp) is { } recipe)
        {
            ProcessGasInput(ent, recipe);
            ProcessRecipe(ent, recipe);
        }
        ProcessTemperatureRegulator(ent);
        _ui.ServerSendUiMessage(ent.Owner, CrystallizerUiKey.Key,
            new CrystallizerUpdateGasMixtureMessage(ent.Comp.CrystallizerGasMixture));
        _ui.ServerSendUiMessage(ent.Owner, CrystallizerUiKey.Key,
            new CrystallizerProgressBarMessage(ent.Comp.ProgressBar));
    }

    private void ProcessGasInput(Entity<CrystallizerComponent> ent, CrystallizerRecipePrototype recipe)
    {
        if (!GetPipeMixture(ent, ent.Comp.InletName, out var inlet))
            return;

        var mixture = ent.Comp.CrystallizerGasMixture;
        var availableRequiredMoles = 0f;
        for (var gas = 0; gas < recipe.MinimumRequirements.Length; gas++)
        {
            if (recipe.MinimumRequirements[gas] > 0f && mixture.GetMoles(gas) < recipe.MinimumRequirements[gas] * 2f)
                availableRequiredMoles += inlet.GetMoles(gas);
        }

        if (availableRequiredMoles <= 0f)
            return;

        var added = new GasMixture { Temperature = inlet.Temperature };
        for (var gas = 0; gas < recipe.MinimumRequirements.Length; gas++)
        {
            if (recipe.MinimumRequirements[gas] <= 0f || mixture.GetMoles(gas) >= recipe.MinimumRequirements[gas] * 2f)
                continue;

            var removed = Math.Min(inlet.GetMoles(gas), ent.Comp.GasInput * 0.5f * inlet.GetMoles(gas) / availableRequiredMoles);
            inlet.AdjustMoles(gas, -removed);
            added.AdjustMoles(gas, removed);
        }

        _atmos.Merge(mixture, added);
    }

    private void ProcessRecipe(Entity<CrystallizerComponent> ent, CrystallizerRecipePrototype recipe)
    {
        var mixture = ent.Comp.CrystallizerGasMixture;
        if (mixture.TotalMoles <= 0f)
            return;

        if (MeetsRequirements(mixture, recipe))
        {
            ApplyHeat(mixture, recipe, ent.Comp);
            var rate = 5f / MathF.Max(MathF.Log10(ent.Comp.TotalRecipeMoles * 0.1f), 0.01f);
            ent.Comp.ProgressBar = Math.Min(ent.Comp.ProgressBar + MinProgressAmount * rate, 100f);
        }
        else
        {
            ent.Comp.QualityLoss = Math.Min(ent.Comp.QualityLoss + 0.5f, 100f);
            ent.Comp.ProgressBar = Math.Max(ent.Comp.ProgressBar - 1f, 0f);
        }

        if (ent.Comp.ProgressBar >= 100f)
            CompleteRecipe(ent, recipe);
    }

    private static bool MeetsRequirements(GasMixture mixture, CrystallizerRecipePrototype recipe)
    {
        if (mixture.Temperature < recipe.MinimumTemperature * MinDeviationRate || mixture.Temperature > recipe.MaximumTemperature * MaxDeviationRate)
            return false;

        for (var gas = 0; gas < recipe.MinimumRequirements.Length; gas++)
        {
            if (mixture.GetMoles(gas) < recipe.MinimumRequirements[gas])
                return false;
        }

        return true;
    }

    private void ApplyHeat(GasMixture mixture, CrystallizerRecipePrototype recipe, CrystallizerComponent component)
    {
        var qualityRate = MinProgressAmount * 4.5f / MathF.Max(MathF.Log10(component.TotalRecipeMoles * 0.1f), 0.01f);
        var median = (recipe.MinimumTemperature + recipe.MaximumTemperature) / 2f;
        if ((mixture.Temperature >= recipe.MinimumTemperature * MinDeviationRate && mixture.Temperature <= recipe.MinimumTemperature) ||
            (mixture.Temperature >= recipe.MaximumTemperature && mixture.Temperature <= recipe.MaximumTemperature * MaxDeviationRate))
            component.QualityLoss = Math.Min(component.QualityLoss + qualityRate, 100f);
        if (mixture.Temperature >= median * MinDeviationRate && mixture.Temperature <= median * MaxDeviationRate)
            component.QualityLoss = Math.Max(component.QualityLoss - qualityRate, -25f);

        var heatCapacity = _atmos.GetHeatCapacity(mixture, true);
        if (heatCapacity > 0f)
            mixture.Temperature = Math.Max(mixture.Temperature + recipe.EnergyRelease / heatCapacity, Atmospherics.TCMB);
    }

    private void CompleteRecipe(Entity<CrystallizerComponent> ent, CrystallizerRecipePrototype recipe)
    {
        var mixture = ent.Comp.CrystallizerGasMixture;
        for (var gas = 0; gas < recipe.MinimumRequirements.Length; gas++)
        {
            var required = recipe.MinimumRequirements[gas];
            if (required <= 0f)
                continue;

            var available = mixture.GetMoles(gas);
            var consumed = required * (1f + ent.Comp.QualityLoss * 0.01f);
            if (available < consumed)
                ent.Comp.QualityLoss = Math.Min(ent.Comp.QualityLoss + 10f, 100f);
            mixture.AdjustMoles(gas, -Math.Min(available, consumed));
        }

        var transform = Transform(ent);
        if (transform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            var tile = _map.CoordinatesToTile(grid, mapGrid, _transform.GetMapCoordinates(ent));
            var coordinates = _map.GridTileToLocal(grid, mapGrid, tile + new Vector2i(0, -1));
            foreach (var (product, amount) in recipe.Products)
            for (var i = 0; i < amount; i++)
                Spawn(product, coordinates);
        }

        ent.Comp.ProgressBar = 0f;
        ent.Comp.QualityLoss = 0f;
    }

    private void ProcessTemperatureRegulator(Entity<CrystallizerComponent> ent)
    {
        if (!GetPipeMixture(ent, ent.Comp.RegulatorName, out var regulator))
            return;

        var mixture = ent.Comp.CrystallizerGasMixture;
        var regulatorCapacity = _atmos.GetHeatCapacity(regulator, true);
        var mixtureCapacity = _atmos.GetHeatCapacity(mixture, true);
        if (regulator.TotalMoles < 0.01f || mixture.TotalMoles <= 0f || regulatorCapacity < Atmospherics.MinimumHeatCapacity || mixtureCapacity < Atmospherics.MinimumHeatCapacity)
            return;

        var heat = 0.95f * (regulator.Temperature - mixture.Temperature) * regulatorCapacity * mixtureCapacity / (regulatorCapacity + mixtureCapacity);
        regulator.Temperature = Math.Max(regulator.Temperature - heat / regulatorCapacity, Atmospherics.TCMB);
        mixture.Temperature = Math.Max(mixture.Temperature + heat / mixtureCapacity, Atmospherics.TCMB);
    }

    private CrystallizerRecipePrototype? GetRecipe(CrystallizerComponent component) =>
        component.SelectedRecipeId is { } id && _prototypes.TryIndex<CrystallizerRecipePrototype>(id, out var recipe) ? recipe : null;

    private bool GetPipeMixture(Entity<CrystallizerComponent> ent, string nodeName, out GasMixture mixture)
    {
        mixture = default!;
        if (!_nodes.TryGetNode(ent.Owner, nodeName, out PipeNode? node))
            return false;
        mixture = node.Air;
        return true;
    }

    private void UpdateUi(Entity<CrystallizerComponent> ent)
    {
        if (!TryComp<ApcPowerReceiverComponent>(ent, out var power))
            return;
        _ui.SetUiState(ent.Owner, CrystallizerUiKey.Key, new CrystallizerBoundUserInterfaceState(
            !power.PowerDisabled, ent.Comp.SelectedRecipeId, ent.Comp.GasInput, ent.Comp.CrystallizerGasMixture,
            ent.Comp.ProgressBar, ent.Comp.QualityLoss));
    }
}

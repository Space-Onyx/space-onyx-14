// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Kitchen.Components;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Power.Components;
using Content.Shared.SmartFridge;

namespace Content.Shared._Onyx.Construction;

public sealed partial class MachinePartEffectsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChargerComponent, MachinePartsChangedEvent>(OnChargerPartsChanged);
        SubscribeLocalEvent<ApcPowerReceiverBatteryComponent, MachinePartsChangedEvent>(OnInternalBatteryPartsChanged);
        SubscribeLocalEvent<GasThermoMachineComponent, MachinePartsChangedEvent>(OnThermoPartsChanged);
        SubscribeLocalEvent<PlantTrayComponent, MachinePartsChangedEvent>(OnPlantTrayPartsChanged);
        SubscribeLocalEvent<SeedExtractorComponent, MachinePartsChangedEvent>(OnSeedExtractorPartsChanged);
        SubscribeLocalEvent<ReagentGrinderComponent, MachinePartsChangedEvent>(OnGrinderPartsChanged);
        SubscribeLocalEvent<CryoPodComponent, MachinePartsChangedEvent>(OnCryoPartsChanged);
        SubscribeLocalEvent<SmartFridgeComponent, MachinePartsChangedEvent>(OnSmartFridgePartsChanged);
        SubscribeLocalEvent<ChargerComponent, MachineUpgradeExamineEvent>(OnChargerExamine);
        SubscribeLocalEvent<GasThermoMachineComponent, MachineUpgradeExamineEvent>(OnThermoExamine);
        SubscribeLocalEvent<PlantTrayComponent, MachineUpgradeExamineEvent>(OnPlantTrayExamine);
        SubscribeLocalEvent<SeedExtractorComponent, MachineUpgradeExamineEvent>(OnSeedExtractorExamine);
        SubscribeLocalEvent<ReagentGrinderComponent, MachineUpgradeExamineEvent>(OnGrinderExamine);
        SubscribeLocalEvent<CryoPodComponent, MachineUpgradeExamineEvent>(OnCryoExamine);
        SubscribeLocalEvent<SmartFridgeComponent, MachineUpgradeExamineEvent>(OnSmartFridgeExamine);
    }

    private void OnChargerPartsChanged(Entity<ChargerComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.ChargeRate = GetBaseline(baseline, "charger-rate", ent.Comp.ChargeRate) * Positive(args.GetRating(MachinePartKind.Capacitor));
        Dirty(ent);
    }

    private void OnInternalBatteryPartsChanged(Entity<ApcPowerReceiverBatteryComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.BatteryRechargeRate = GetBaseline(baseline, "battery-recharge", ent.Comp.BatteryRechargeRate)
            * Positive(args.GetRating(MachinePartKind.Capacitor));
    }

    private void OnThermoPartsChanged(Entity<GasThermoMachineComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        var matter = Positive(args.GetRating(MachinePartKind.MatterBin));
        ent.Comp.HeatCapacity = GetBaseline(baseline, "thermo-capacity", ent.Comp.HeatCapacity) * matter;
        var rangeBonus = (args.GetRating(MachinePartKind.Laser) - 1f) * 30f;
        ent.Comp.MinTemperature = Math.Max(Atmospherics.TCMB, GetBaseline(baseline, "thermo-min", ent.Comp.MinTemperature) - rangeBonus);
        ent.Comp.MaxTemperature = GetBaseline(baseline, "thermo-max", ent.Comp.MaxTemperature) + rangeBonus;
        ent.Comp.TargetTemperature = Math.Clamp(ent.Comp.TargetTemperature, ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
    }

    private void OnPlantTrayPartsChanged(Entity<PlantTrayComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        var matter = Positive(args.GetRating(MachinePartKind.MatterBin));
        ent.Comp.MaxWaterLevel = GetBaseline(baseline, "tray-water", ent.Comp.MaxWaterLevel) * matter;
        ent.Comp.MaxNutritionLevel = GetBaseline(baseline, "tray-nutrition", ent.Comp.MaxNutritionLevel) * matter;
        ent.Comp.TrayConsumptionMultiplier = GetBaseline(baseline, "tray-consumption", ent.Comp.TrayConsumptionMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Servo), 0.1f);
        Dirty(ent);
    }

    private void OnGrinderPartsChanged(Entity<ReagentGrinderComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.WorkTimeMultiplier = GetBaseline(baseline, "grinder-time", ent.Comp.WorkTimeMultiplier)
            / Math.Max(1f, args.GetRating(MachinePartKind.Servo));
        ent.Comp.StorageMaxEntities = (int) MathF.Round(GetBaseline(baseline, "grinder-capacity", ent.Comp.StorageMaxEntities) * Positive(args.GetRating(MachinePartKind.MatterBin)));
        Dirty(ent);
    }

    private void OnSeedExtractorPartsChanged(Entity<SeedExtractorComponent> ent, ref MachinePartsChangedEvent args)
    {
        ent.Comp.SeedMultiplier = Math.Max(1f, args.GetRating(MachinePartKind.Servo));
    }

    private void OnCryoPartsChanged(Entity<CryoPodComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.BeakerTransferAmount = GetBaseline(baseline, "cryo-amount", ent.Comp.BeakerTransferAmount.Float()) * Positive(args.GetRating(MachinePartKind.MatterBin));
        ent.Comp.CoolingEfficiency = Positive(args.GetRating(MachinePartKind.Laser));
    }

    private void OnSmartFridgePartsChanged(Entity<SmartFridgeComponent> ent, ref MachinePartsChangedEvent args)
    {
        ent.Comp.Capacity = (int) MathF.Round(ent.Comp.BaseCapacity * Positive(args.GetRating(MachinePartKind.MatterBin)));
        Dirty(ent);
    }

    private void OnChargerExamine(Entity<ChargerComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            args.Add("machine-upgrade-charging-speed", ent.Comp.ChargeRate / baseline.Values["charger-rate"]);
    }

    private void OnThermoExamine(Entity<GasThermoMachineComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-thermomachine-heat-capacity", ent.Comp.HeatCapacity / baseline.Values["thermo-capacity"]);
        args.Add("machine-upgrade-thermomachine-temp-range",
            (ent.Comp.MaxTemperature - ent.Comp.MinTemperature) / (baseline.Values["thermo-max"] - baseline.Values["thermo-min"]));
    }

    private void OnPlantTrayExamine(Entity<PlantTrayComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-hydro-water", ent.Comp.MaxWaterLevel / baseline.Values["tray-water"]);
        args.Add("machine-upgrade-hydro-nutrition", ent.Comp.MaxNutritionLevel / baseline.Values["tray-nutrition"]);
        args.Add("machine-upgrade-hydro-nutrition-consume", ent.Comp.TrayConsumptionMultiplier / baseline.Values["tray-consumption"]);
    }

    private static void OnSeedExtractorExamine(Entity<SeedExtractorComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        args.Add("machine-upgrade-seed-extraction", ent.Comp.SeedMultiplier);
    }

    private void OnGrinderExamine(Entity<ReagentGrinderComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-process-speed", baseline.Values["grinder-time"] / ent.Comp.WorkTimeMultiplier);
        args.Add("machine-upgrade-capacity", ent.Comp.StorageMaxEntities / baseline.Values["grinder-capacity"]);
    }

    private void OnCryoExamine(Entity<CryoPodComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-cryo-transfer", ent.Comp.BeakerTransferAmount.Float() / baseline.Values["cryo-amount"]);
        args.Add("machine-upgrade-cryo-cooling", ent.Comp.CoolingEfficiency);
    }

    private static void OnSmartFridgeExamine(Entity<SmartFridgeComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        args.Add("machine-upgrade-smartfridge-capacity", (float) ent.Comp.Capacity / ent.Comp.BaseCapacity);
    }

    private static float Positive(float tier)
    {
        return Math.Max(1f, tier);
    }

    private static float LinearDecrease(float tier, float step)
    {
        return Math.Clamp(1f - (tier - 1f) * step, 0.5f, 1f);
    }

    private static float GetBaseline(MachinePartBaselineComponent component, string key, float value)
    {
        if (component.Values.TryGetValue(key, out var baseline))
            return baseline;

        component.Values[key] = value;
        return value;
    }
}

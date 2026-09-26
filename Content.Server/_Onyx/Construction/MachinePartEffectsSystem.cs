// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Atmos.Portable;
using Content.Server.Power.Components;
using Content.Server.Kitchen.Components;
using Content.Server.Power.SMES;
using Content.Shared._Onyx.Construction;
using Content.Shared.Radio.Components;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Bed.Components;
using Content.Server._Onyx.Chemistry.Components;
using Content.Server._Onyx.Telecommunications.Components;

namespace Content.Server._Onyx.Construction;

public sealed partial class MachinePartEffectsSystem : EntitySystem
{
    [Dependency] private Content.Server._Onyx.Bitrunning.Systems.QuantumConsoleSystem _quantumConsole = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceHeaterComponent, MachinePartsChangedEvent>(OnSpaceHeaterPartsChanged);
        SubscribeLocalEvent<MicrowaveComponent, MachinePartsChangedEvent>(OnMicrowavePartsChanged);
        SubscribeLocalEvent<StasisBedComponent, MachinePartsChangedEvent>(OnStasisPartsChanged);
        SubscribeLocalEvent<SmesComponent, MachinePartsChangedEvent>(OnSmesPartsChanged);
        SubscribeLocalEvent<QuantumServerComponent, MachinePartsChangedEvent>(OnQuantumServerPartsChanged);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, MachinePartsChangedEvent>(OnEnergyDispenserPartsChanged);
        SubscribeLocalEvent<TelecomReceiverComponent, MachinePartsChangedEvent>(OnTelecomReceiverPartsChanged);
        SubscribeLocalEvent<TelecomProcessorComponent, MachinePartsChangedEvent>(OnTelecomProcessorPartsChanged);
        SubscribeLocalEvent<TelecomBroadcasterComponent, MachinePartsChangedEvent>(OnTelecomBroadcasterPartsChanged);
        SubscribeLocalEvent<TelecomHubComponent, MachinePartsChangedEvent>(OnTelecomHubPartsChanged);
        SubscribeLocalEvent<SpaceHeaterComponent, MachineUpgradeExamineEvent>(OnSpaceHeaterExamine);
        SubscribeLocalEvent<MicrowaveComponent, MachineUpgradeExamineEvent>(OnMicrowaveExamine);
        SubscribeLocalEvent<StasisBedComponent, MachineUpgradeExamineEvent>(OnStasisExamine);
        SubscribeLocalEvent<SmesComponent, MachineUpgradeExamineEvent>(OnSmesExamine);
        SubscribeLocalEvent<QuantumServerComponent, MachineUpgradeExamineEvent>(OnQuantumServerExamine);
        SubscribeLocalEvent<EnergyReagentDispenserComponent, MachineUpgradeExamineEvent>(OnEnergyDispenserExamine);
        SubscribeLocalEvent<TelecomReceiverComponent, MachineUpgradeExamineEvent>(OnTelecomReceiverExamine);
        SubscribeLocalEvent<TelecomProcessorComponent, MachineUpgradeExamineEvent>(OnTelecomProcessorExamine);
        SubscribeLocalEvent<TelecomBroadcasterComponent, MachineUpgradeExamineEvent>(OnTelecomBroadcasterExamine);
        SubscribeLocalEvent<TelecomHubComponent, MachineUpgradeExamineEvent>(OnTelecomHubExamine);
    }

    private void OnSpaceHeaterPartsChanged(Entity<SpaceHeaterComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        var capacitor = Positive(args.GetRating(MachinePartKind.Capacitor));
        ent.Comp.PowerConsumption = GetBaseline(baseline, "heater-power", ent.Comp.PowerConsumption) * capacitor;
        var rangeBonus = (args.GetRating(MachinePartKind.Laser) - 1f) * 10f;
        ent.Comp.MinTemperature = GetBaseline(baseline, "heater-min", ent.Comp.MinTemperature) - rangeBonus;
        ent.Comp.MaxTemperature = GetBaseline(baseline, "heater-max", ent.Comp.MaxTemperature) + rangeBonus;
        if (TryComp<GasThermoMachineComponent>(ent, out var thermo))
            thermo.HeatCapacity = ent.Comp.PowerConsumption;
    }

    private void OnMicrowavePartsChanged(Entity<MicrowaveComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.CookTimeMultiplier = GetBaseline(baseline, "microwave-time", ent.Comp.CookTimeMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Laser), 0.1f);
        ent.Comp.Capacity = (int) MathF.Round(GetBaseline(baseline, "microwave-capacity", ent.Comp.Capacity) * Positive(args.GetRating(MachinePartKind.MatterBin)));
        ent.Comp.ExplosionChance = Math.Max(0f, GetBaseline(baseline, "microwave-explosion", ent.Comp.ExplosionChance)
            - (args.GetRating(MachinePartKind.Laser) - 1f) * 0.05f);
    }

    private void OnStasisPartsChanged(Entity<StasisBedComponent> ent, ref MachinePartsChangedEvent args)
    {
        if (!TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            return;

        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        receiver.Load = GetBaseline(baseline, "stasis-power", receiver.Load)
            * LinearDecrease(args.GetRating(MachinePartKind.Capacitor), 0.1f);
    }

    private void OnSmesPartsChanged(Entity<SmesComponent> ent, ref MachinePartsChangedEvent args)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var battery))
            return;

        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        var capacitor = 1f + args.GetTierBonusSum(MachinePartKind.Capacitor);
        battery.MaxChargeRate = GetBaseline(baseline, "smes-input", battery.MaxChargeRate) * capacitor;
        battery.MaxSupply = GetBaseline(baseline, "smes-output", battery.MaxSupply) * capacitor;
    }

    private void OnQuantumServerPartsChanged(Entity<QuantumServerComponent> ent, ref MachinePartsChangedEvent args)
    {
        var previousScannerTier = ent.Comp.ScannerTier;
        ent.Comp.CooldownMultiplier = Math.Clamp(1.15f - args.GetRating(MachinePartKind.Capacitor) * 0.15f, 0.1f, 1f);
        ent.Comp.ScannerTier = Math.Max(1, (int) MathF.Round(args.GetRating(MachinePartKind.Scanner)));
        var servoBonus = args.GetTierBonusSum(MachinePartKind.Servo) * 0.1f;
        ent.Comp.QualityBonus = servoBonus;
        ent.Comp.FinalExitDamageMultiplier = Math.Clamp(1f - servoBonus, 0.2f, 1f);
        Dirty(ent);
        if (previousScannerTier != ent.Comp.ScannerTier)
            _quantumConsole.RefreshServerConsoles(ent);
    }

    private void OnEnergyDispenserPartsChanged(Entity<EnergyReagentDispenserComponent> ent, ref MachinePartsChangedEvent args)
    {
        ent.Comp.EnergyCostMultiplier = LinearDecrease(args.GetRating(MachinePartKind.MatterBin), 0.1f);
    }

    private void OnTelecomReceiverPartsChanged(Entity<TelecomReceiverComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.LossChanceMultiplier = GetBaseline(baseline, "telecom-receiver-loss", ent.Comp.LossChanceMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Capacitor), 0.15f);
        ent.Comp.LossExponent = GetBaseline(baseline, "telecom-receiver-exponent", ent.Comp.LossExponent)
            * Positive(args.GetRating(MachinePartKind.Scanner));
    }

    private void OnTelecomProcessorPartsChanged(Entity<TelecomProcessorComponent> ent, ref MachinePartsChangedEvent args)
    {
        if (HasComp<TelecomServerComponent>(ent.Owner))
            return;

        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.CalibrationLatencyMultiplier = GetBaseline(baseline, "telecom-processor-latency", ent.Comp.CalibrationLatencyMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Servo), 0.15f);
        ent.Comp.GarbleChanceMultiplier = GetBaseline(baseline, "telecom-processor-garble", ent.Comp.GarbleChanceMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Scanner), 0.15f);
    }

    private void OnTelecomBroadcasterPartsChanged(Entity<TelecomBroadcasterComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.OutputLossChanceMultiplier = GetBaseline(baseline, "telecom-broadcaster-loss", ent.Comp.OutputLossChanceMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Laser), 0.15f);
        ent.Comp.DamageLossMultiplier = GetBaseline(baseline, "telecom-broadcaster-damage", ent.Comp.DamageLossMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Capacitor), 0.15f);
    }

    private void OnTelecomHubPartsChanged(Entity<TelecomHubComponent> ent, ref MachinePartsChangedEvent args)
    {
        var baseline = EnsureComp<MachinePartBaselineComponent>(ent);
        ent.Comp.RouteLossChanceMultiplier = GetBaseline(baseline, "telecom-hub-loss", ent.Comp.RouteLossChanceMultiplier)
            * LinearDecrease(args.GetRating(MachinePartKind.Servo), 0.15f);
    }

    private void OnSpaceHeaterExamine(Entity<SpaceHeaterComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-spaceheater-power", ent.Comp.PowerConsumption / baseline.Values["heater-power"]);
        args.Add("machine-upgrade-spaceheater-temp-range",
            (ent.Comp.MaxTemperature - ent.Comp.MinTemperature) / (baseline.Values["heater-max"] - baseline.Values["heater-min"]));
    }

    private void OnMicrowaveExamine(Entity<MicrowaveComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;
        args.Add("machine-upgrade-cook-speed", baseline.Values["microwave-time"] / ent.Comp.CookTimeMultiplier);
        args.Add("machine-upgrade-capacity", ent.Comp.Capacity / baseline.Values["microwave-capacity"]);
        args.Add("machine-upgrade-malfunction-reduction", ent.Comp.ExplosionChance / baseline.Values["microwave-explosion"]);
    }

    private void OnStasisExamine(Entity<StasisBedComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (TryComp<MachinePartBaselineComponent>(ent, out var baseline)
            && TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            args.Add("machine-upgrade-stasis-bed-power", receiver.Load / baseline.Values["stasis-power"]);
    }

    private void OnSmesExamine(Entity<SmesComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline)
            || !TryComp<PowerNetworkBatteryComponent>(ent, out var battery))
            return;
        args.Add("machine-upgrade-power-input", battery.MaxChargeRate / baseline.Values["smes-input"]);
        args.Add("machine-upgrade-power-output", battery.MaxSupply / baseline.Values["smes-output"]);
    }

    private static void OnQuantumServerExamine(Entity<QuantumServerComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        args.Add("machine-upgrade-quantum-cooldown", 1f / Math.Max(ent.Comp.CooldownMultiplier, 0.001f));
        args.Add("machine-upgrade-quantum-reward-bonus", 1f + ent.Comp.QualityBonus);
        args.Add("machine-upgrade-quantum-exit-injury", 1f / Math.Max(ent.Comp.FinalExitDamageMultiplier, 0.001f));
    }

    private static void OnEnergyDispenserExamine(Entity<EnergyReagentDispenserComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        args.Add("machine-upgrade-energy-cost", ent.Comp.EnergyCostMultiplier);
    }

    private void OnTelecomReceiverExamine(Entity<TelecomReceiverComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;

        if (baseline.Values.TryGetValue("telecom-receiver-loss", out var loss))
            args.Add("machine-upgrade-telecom-reception-loss", ent.Comp.LossChanceMultiplier / loss);
        if (baseline.Values.TryGetValue("telecom-receiver-exponent", out var exponent))
            args.Add("machine-upgrade-telecom-reception-clarity", ent.Comp.LossExponent / exponent);
    }

    private void OnTelecomProcessorExamine(Entity<TelecomProcessorComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (HasComp<TelecomServerComponent>(ent.Owner))
            return;

        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;

        if (baseline.Values.TryGetValue("telecom-processor-latency", out var latency))
            args.Add("machine-upgrade-telecom-processing-latency", ent.Comp.CalibrationLatencyMultiplier / latency);
        if (baseline.Values.TryGetValue("telecom-processor-garble", out var garble))
            args.Add("machine-upgrade-telecom-distortion", ent.Comp.GarbleChanceMultiplier / garble);
    }

    private void OnTelecomBroadcasterExamine(Entity<TelecomBroadcasterComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;

        if (baseline.Values.TryGetValue("telecom-broadcaster-loss", out var loss))
            args.Add("machine-upgrade-telecom-broadcast-loss", ent.Comp.OutputLossChanceMultiplier / loss);
        if (baseline.Values.TryGetValue("telecom-broadcaster-damage", out var damage))
            args.Add("machine-upgrade-telecom-cascade-damage", ent.Comp.DamageLossMultiplier / damage);
    }

    private void OnTelecomHubExamine(Entity<TelecomHubComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<MachinePartBaselineComponent>(ent, out var baseline))
            return;

        if (baseline.Values.TryGetValue("telecom-hub-loss", out var loss))
            args.Add("machine-upgrade-telecom-routing-loss", ent.Comp.RouteLossChanceMultiplier / loss);
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

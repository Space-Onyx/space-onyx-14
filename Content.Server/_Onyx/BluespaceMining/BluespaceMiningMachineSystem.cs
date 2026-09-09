using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Flash;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.BluespaceMining;
using Content.Shared.Atmos;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Power;
using Content.Shared.Popups;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.BluespaceMining;

public sealed partial class BluespaceMiningMachineSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private FlashSystem _flash = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private OreSiloSystem _oreSilo = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceMiningMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BluespaceMiningMachineComponent, ItemSlotInsertAttemptEvent>(OnCoreInsertAttempt);
        SubscribeLocalEvent<BluespaceMiningMachineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BluespaceMiningMachineComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<BluespaceMiningMachineComponent, PanelChangedEvent>(OnPanelChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BluespaceMiningMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            machine.ProductionAccumulator += frameTime;
            while (machine.ProductionAccumulator >= 1f)
            {
                machine.ProductionAccumulator -= 1f;
                UpdateAppearance((uid, machine));
                ProcessSecond((uid, machine));
            }
        }
    }

    private void OnStartup(Entity<BluespaceMiningMachineComponent> ent, ref ComponentStartup args) => UpdateAppearance(ent);

    private void OnPowerChanged(Entity<BluespaceMiningMachineComponent> ent, ref PowerChangedEvent args) => UpdateAppearance(ent, args.Powered);

    private void OnPanelChanged(Entity<BluespaceMiningMachineComponent> ent, ref PanelChangedEvent args) => UpdateAppearance(ent);

    private void UpdateAppearance(Entity<BluespaceMiningMachineComponent> ent, bool? powered = null)
    {
        powered = powered is not false && IsOperational(ent);
        var panelOpen = TryComp(ent.Owner, out WiresPanelComponent? panel) && panel.Open;
        var state = (powered.Value, panelOpen) switch
        {
            (true, false) => BluespaceMiningMachineVisualState.Working,
            (true, true) => BluespaceMiningMachineVisualState.Maintenance,
            (false, true) => BluespaceMiningMachineVisualState.UnpoweredMaintenance,
            _ => BluespaceMiningMachineVisualState.Unpowered,
        };
        _appearance.SetData(ent, BluespaceMiningMachineVisuals.State, state);
    }

    private void OnCoreInsertAttempt(Entity<BluespaceMiningMachineComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID == BluespaceMiningMachineComponent.CoreSlotId)
        {
            EnsureComp<BluespaceMiningCoreComponent>(args.Item);
            ent.Comp.HalfIntegrityWarningAccumulator = 0f;
            ent.Comp.CriticalIntegrityWarningAccumulator = 0f;
            ent.Comp.WarnedHalfIntegrity = false;
            ent.Comp.WarnedCriticalIntegrity = false;
        }
    }

    private void OnExamined(Entity<BluespaceMiningMachineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("bluespace-mining-machine-examine-efficiency"));
        var coreUid = _itemSlots.GetItemOrNull(ent.Owner, BluespaceMiningMachineComponent.CoreSlotId);
        if (coreUid is null || !TryComp(coreUid, out BluespaceMiningCoreComponent? core))
        {
            args.PushMarkup(Loc.GetString("bluespace-mining-machine-examine-no-core"));
            return;
        }

        var percent = MathF.Round(core.Integrity * 100f, 1);
        args.PushMarkup(Loc.GetString("bluespace-mining-machine-examine-core", ("integrity", percent)));
        args.PushMarkup(Loc.GetString($"bluespace-mining-machine-examine-instability-{GetInstabilityLevel(core.Integrity)}"));

        if (!Transform(ent).Anchored)
            args.PushMarkup(Loc.GetString("bluespace-mining-machine-examine-unanchored"));
        if (!TryComp(ent.Owner, out OreSiloClientComponent? client) || client.Silo is null)
            args.PushMarkup(Loc.GetString("bluespace-mining-machine-examine-no-silo"));
    }

    private static int GetInstabilityLevel(float integrity)
    {
        if (integrity <= 0.1f)
            return 3;
        if (integrity <= 0.5f)
            return 2;
        if (integrity <= 0.99f)
            return 1;
        return 0;
    }

    private void ProcessSecond(Entity<BluespaceMiningMachineComponent> ent)
    {
        if (!TryGetOperationalParts(ent, out var core, out var silo))
            return;

        DamageCore(ent, core);
        if (core.Comp.Integrity <= 0f)
            return;
        AffectAmbientTemperature(ent);
        ProcessInstability(ent, core);

        if (ent.Comp.Production.Count == 0)
            return;

        var selectedIndex = _random.Next(ent.Comp.Production.Count);
        foreach (var (material, rate) in ent.Comp.Production)
        {
            if (selectedIndex-- != 0)
                continue;

            _materialStorage.TryChangeMaterialAmount(silo, material, (int) MathF.Round(rate * 1000f));
            break;
        }
    }

    private void ProcessInstability(
        Entity<BluespaceMiningMachineComponent> ent,
        Entity<BluespaceMiningCoreComponent> core)
    {
        if (GetInstabilityLevel(core.Comp.Integrity) == 0)
        {
            ent.Comp.InstabilityAccumulator = 0f;
            return;
        }

        ent.Comp.InstabilityAccumulator += 1f;
        if (ent.Comp.InstabilityAccumulator < ent.Comp.InstabilityCooldown)
            return;
        ent.Comp.InstabilityAccumulator = 0f;

        switch (GetInstabilityLevel(core.Comp.Integrity))
        {
            case 1:
                TriggerLowInstability(ent, core);
                break;
            case 2:
                TriggerMediumInstability(ent);
                break;
            case 3:
                TriggerHighInstability(ent);
                break;
        }
    }

    private void TriggerLowInstability(
        Entity<BluespaceMiningMachineComponent> ent,
        Entity<BluespaceMiningCoreComponent> core)
    {
        var totalWeight = ent.Comp.LowInstabilityNoSpawnWeight + ent.Comp.LowInstabilityCoreRepairWeight;
        foreach (var weight in ent.Comp.LowInstabilitySpawns.Values)
            totalWeight += weight;

        var selected = _random.NextFloat() * totalWeight;
        if (selected < ent.Comp.LowInstabilityCoreRepairWeight)
        {
            core.Comp.Integrity = MathF.Min(1f, core.Comp.Integrity + ent.Comp.LowInstabilityCoreRepair);
            Dirty(core);
            _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-repair"), ent.Owner);
            return;
        }
        selected -= ent.Comp.LowInstabilityCoreRepairWeight;

        foreach (var (prototype, weight) in ent.Comp.LowInstabilitySpawns)
        {
            selected -= weight;
            if (selected > 0f)
                continue;

            Spawn(prototype, Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-low"), ent.Owner);
            return;
        }
    }

    private void TriggerMediumInstability(Entity<BluespaceMiningMachineComponent> ent)
    {
        if (_atmosphere.GetContainingMixture(ent.Owner, excite: true) is not { } air)
            return;

        _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-medium"), ent.Owner, PopupType.MediumCaution);

        switch (_random.Next(60))
        {
            case < 5:
                air.AdjustMoles(Gas.Plasma, _random.Next(15, 36));
                break;
            case < 15:
                air.AdjustMoles(Gas.Nitrogen, _random.Next(45, 86));
                break;
            case < 25:
                air.AdjustMoles(Gas.CarbonDioxide, _random.Next(20, 46));
                break;
            case < 35:
                air.AdjustMoles(Gas.WaterVapor, _random.Next(25, 56));
                break;
            case < 45:
                air.Temperature = MathF.Max(air.Temperature - _random.Next(75, 131), 7.5f);
                break;
            case < 55:
                air.AdjustMoles(Gas.NitrousOxide, _random.Next(8, 19));
                break;
            default:
                _flash.FlashArea(ent.Owner, null, 7f, TimeSpan.FromSeconds(4), displayPopup: true, flashbang: true);
                break;
        }
    }

    private void TriggerHighInstability(Entity<BluespaceMiningMachineComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-high"), ent.Owner, PopupType.LargeCaution);
        switch (_random.Next(92))
        {
            case < 60:
                Spawn(ent.Comp.HighAnomalySpawn, Transform(ent).Coordinates);
                break;
            case < 70:
                Spawn(ent.Comp.HighHostileSpawn, Transform(ent).Coordinates);
                break;
            case < 75:
                SpawnAimedMeteor(ent);
                break;
            case < 90:
                BeginCatastrophicFailure(ent);
                break;
            default:
                Spawn(ent.Comp.HighRareItemSpawn, Transform(ent).Coordinates);
                break;
        }
    }

    private void SpawnAimedMeteor(Entity<BluespaceMiningMachineComponent> ent)
    {
        var target = _transform.GetMapCoordinates(ent);
        var offset = _random.NextAngle().RotateVec(new Vector2(ent.Comp.HighMeteorDistance, 0f));
        var meteor = Spawn(ent.Comp.HighMeteorSpawn, new MapCoordinates(target.Position + offset, target.MapId));
        if (TryComp(meteor, out PhysicsComponent? physics))
            _physics.ApplyLinearImpulse(meteor, -offset.Normalized() * ent.Comp.HighMeteorVelocity * physics.Mass, body: physics);
    }

    private void BeginCatastrophicFailure(Entity<BluespaceMiningMachineComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-catastrophic"), ent.Owner, PopupType.LargeCaution);
        Timer.Spawn(TimeSpan.FromSeconds(2), () =>
        {
            if (TerminatingOrDeleted(ent.Owner))
                return;

            _explosion.QueueExplosion(ent.Owner, "Default", 2f, 4f, 5f, 8f);
            QueueDel(ent.Owner);
        });
    }

    private void AffectAmbientTemperature(Entity<BluespaceMiningMachineComponent> ent)
    {
        if (!_random.Prob(ent.Comp.AmbientTemperatureEffectChance * 100f))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return;

        var position = xform.Coordinates.Position;
        var tiles = new List<TileRef>();
        var tileEnumerator = _map.GetLocalTilesIntersecting(gridUid, grid,
            new Box2(position - new Vector2(2f), position + new Vector2(2f)));
        while (tileEnumerator.MoveNext(out var tile))
            tiles.Add(tile);
        _random.Shuffle(tiles);

        var count = Math.Min(_random.Next(1, 4), tiles.Count);
        for (var i = 0; i < count; i++)
        {
            var air = _atmosphere.GetTileMixture(xform.GridUid, xform.MapUid, tiles[i].GridIndices, true);
            if (air is null)
                continue;

            air.Temperature = _random.Prob(50f)
                ? MathF.Min(air.Temperature + _random.Next(25, 181), 723.15f)
                : MathF.Max(air.Temperature - _random.Next(25, 141), 17.5f);
        }
    }

    private void DamageCore(Entity<BluespaceMiningMachineComponent> machine, Entity<BluespaceMiningCoreComponent> core)
    {
        if (_random.Prob(5f))
            return;

        var multiplier = 1f;
        if (_atmosphere.GetContainingMixture(machine.Owner, excite: true) is { } air)
        {
            if (air.Temperature < 263.15f || air.Temperature > 283.15f)
                multiplier *= 2f;
            if (air.Pressure < 51.28f || air.Pressure > 151.28f)
                multiplier *= 2f;
        }

        core.Comp.Integrity = MathF.Max(0f, core.Comp.Integrity - multiplier / machine.Comp.CoreLifetime);
        Dirty(core);
        WarnCoreIntegrity(machine, core.Comp.Integrity);
        if (core.Comp.Integrity <= 0f)
            QueueDel(core);
    }

    private void WarnCoreIntegrity(Entity<BluespaceMiningMachineComponent> machine, float integrity)
    {
        if (integrity <= 0.1f)
        {
            machine.Comp.CriticalIntegrityWarningAccumulator += 1f;
            if (!machine.Comp.WarnedCriticalIntegrity || machine.Comp.CriticalIntegrityWarningAccumulator >= 60f)
            {
                machine.Comp.WarnedCriticalIntegrity = true;
                machine.Comp.CriticalIntegrityWarningAccumulator = 0f;
                _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-core-critical"), machine.Owner, PopupType.LargeCaution);
            }
        }
        else if (integrity <= 0.5f)
        {
            machine.Comp.HalfIntegrityWarningAccumulator += 1f;
            if (!machine.Comp.WarnedHalfIntegrity || machine.Comp.HalfIntegrityWarningAccumulator >= 60f)
            {
                machine.Comp.WarnedHalfIntegrity = true;
                machine.Comp.HalfIntegrityWarningAccumulator = 0f;
                _popup.PopupEntity(Loc.GetString("bluespace-mining-machine-popup-core-damaged"), machine.Owner, PopupType.MediumCaution);
            }
        }
    }

    private bool TryGetOperationalParts(
        Entity<BluespaceMiningMachineComponent> ent,
        out Entity<BluespaceMiningCoreComponent> core,
        out EntityUid silo)
    {
        core = default;
        silo = default;

        if (!_power.IsPowered(ent) || !Transform(ent).Anchored ||
            _itemSlots.GetItemOrNull(ent.Owner, BluespaceMiningMachineComponent.CoreSlotId) is not { } coreUid ||
            !TryComp(coreUid, out BluespaceMiningCoreComponent? coreComponent) ||
            !TryComp(ent.Owner, out OreSiloClientComponent? client) || client.Silo is not { } siloUid ||
            !_oreSilo.CanTransmitMaterials(siloUid, ent.Owner))
        {
            return false;
        }

        core = (coreUid, coreComponent);
        silo = siloUid;
        return true;
    }

    private bool IsOperational(Entity<BluespaceMiningMachineComponent> ent) =>
        TryGetOperationalParts(ent, out _, out _);
}

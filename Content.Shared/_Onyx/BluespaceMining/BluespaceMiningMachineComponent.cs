using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.BluespaceMining;

[RegisterComponent]
public sealed partial class BluespaceMiningMachineComponent : Component
{
    public const string CoreSlotId = "bluespace-core";

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, float> Production = new();

    [DataField]
    public float CoreLifetime = 1800f;

    [DataField]
    public float InstabilityCooldown = 300f;

    [DataField]
    public float AmbientTemperatureEffectChance = 0.02f;

    [DataField]
    public Dictionary<EntProtoId, float> LowInstabilitySpawns = new();

    [DataField]
    public float LowInstabilityNoSpawnWeight = 25f;

    [DataField]
    public float LowInstabilityCoreRepairWeight = 5f;

    [DataField]
    public float LowInstabilityCoreRepair = 0.1f;

    [DataField]
    public EntProtoId HighAnomalySpawn = "RandomAnomalySpawner";

    [DataField]
    public EntProtoId HighRareItemSpawn = "WeaponPulseRifle";

    [DataField]
    public EntProtoId HighMeteorSpawn = "MeteorLarge";

    [DataField]
    public EntProtoId HighHostileSpawn = "MobCarp";

    [DataField]
    public float HighMeteorDistance = 35f;

    [DataField]
    public float HighMeteorVelocity = 18f;

    public float ProductionAccumulator;

    public float InstabilityAccumulator;

    public float HalfIntegrityWarningAccumulator;

    public float CriticalIntegrityWarningAccumulator;

    public bool WarnedHalfIntegrity;

    public bool WarnedCriticalIntegrity;
}

[Serializable, NetSerializable]
public enum BluespaceMiningMachineVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum BluespaceMiningMachineVisualState : byte
{
    Working,
    Unpowered,
    Maintenance,
    UnpoweredMaintenance,
}

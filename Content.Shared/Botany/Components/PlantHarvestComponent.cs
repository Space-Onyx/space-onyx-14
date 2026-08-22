using Content.Shared.Botany.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Analyzers;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Component for plant harvesting process.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)] // <Onyx-SeedDna-edited>
[Access(typeof(PlantHarvestSystem), Other = AccessPermissions.ReadWriteExecute)] // <Onyx-SeedDna-edited>
public sealed partial class PlantHarvestComponent : Component
{
    /// <summary>
    /// Harvest repeat type.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HarvestType HarvestRepeat = HarvestType.NoRepeat;
}

/// <summary>
/// Harvest options for plants.
/// </summary>
[Serializable, NetSerializable]
public enum HarvestType
{
    /// <summary>
    /// Plant is removed on harvest.
    /// </summary>
    NoRepeat,

    /// <summary>
    /// Plant makes produce every Production ticks.
    /// </summary>
    Repeat,

    /// <summary>
    /// Repeat, plus produce is dropped on the ground near the plant automatically.
    /// </summary>
    SelfHarvest
}

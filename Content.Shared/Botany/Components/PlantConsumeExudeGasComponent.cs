using Content.Shared.Atmos;
using Content.Shared.Botany.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Analyzers; // <Onyx-SeedDna>

namespace Content.Shared.Botany.Components;

/// <summary>
/// Component for gas to consume/exude on plant growth.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
[Access(typeof(SharedPlantConsumeExudeGasSystem), Other = AccessPermissions.ReadWriteExecute)] // <Onyx-SeedDna-edited>
public sealed partial class PlantConsumeExudeGasComponent : Component
{
    /// <summary>
    /// Dictionary of gases and their consumption rates per growth tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Gas, float> ConsumeGasses = new();

    /// <summary>
    /// Dictionary of gases and their exude rates per growth tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Gas, float> ExudeGasses = new();
}

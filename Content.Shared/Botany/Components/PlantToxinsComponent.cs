using Content.Shared.Botany.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Analyzers;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Component for plant resistance to toxins.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(PlantToxinsSystem), Other = AccessPermissions.ReadWriteExecute)] // <Onyx-SeedDna-edited>
public sealed partial class PlantToxinsComponent : Component
{
    /// <summary>
    /// Maximum toxin level the plant can tolerate before taking damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ToxinsTolerance = 4f;

    /// <summary>
    /// Divisor for calculating toxin uptake rate. Higher values mean slower toxin processing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ToxinUptakeDivisor = 10f;
}

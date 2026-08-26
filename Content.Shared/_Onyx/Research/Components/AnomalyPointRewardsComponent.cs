using Content.Shared._Onyx.Research;
using Robust.Shared.GameStates;

namespace Content.Shared.Research.Components;

/// <summary>
/// Redirects the research output of an anomaly (through its connected vessel)
/// into the given point types instead of the default General type.
/// Entry amounts act as proportional weights of the anomaly's computed point value,
/// so severity, health and behavior modifiers keep applying.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnomalyPointRewardsComponent : Component
{
    /// <summary>
    /// Weighted distribution of the anomaly's point output by point type.
    /// Empty by default, keeping anomalies on the legacy General output.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ResearchPointAmount> PointTypes = new();
}

using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Wagging;

/// <summary>
/// An emoting wag for markings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(WaggingSystem), typeof(Content.Shared._Onyx.Wagging.TransplantedTailWaggingSystem))]
public sealed partial class WaggingComponent : Component
{
    public const string DefaultSuffix = "Animated";
    /// <summary>
    /// The prototype id of the wagging action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionToggleWagging";

    /// <summary>
    /// Reference to the action entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// The visual layer of the tail marking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HumanoidVisualLayers Layer = HumanoidVisualLayers.Tail;

    /// <summary>
    /// The organ category to which the tail is attached.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<OrganCategoryPrototype> Organ = "Torso";

    /// <summary>
    /// The suffix to add to get the animated marking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Suffix = DefaultSuffix;

    /// <summary>
    /// Whether the entity is currently wagging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Wagging = false;
}

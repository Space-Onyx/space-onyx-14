using Content.Shared.Hands.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Body;

/// <summary>
/// Organs with this component provide a hand with the given ID and data to the body when inserted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // <Onyx-FlexibleAnatomy-edited>
[Access(typeof(HandOrganSystem))]
public sealed partial class HandOrganComponent : Component
{
    /// <summary>
    /// The hand ID used by <seealso cref="HandsComponent" /> on the body
    /// </summary>
    [DataField(required: true)]
    public string HandID;

    // <Onyx-FlexibleAnatomy>
    [DataField, AutoNetworkedField]
    public string? RuntimeHandID;
    // </Onyx-FlexibleAnatomy>

    /// <summary>
    /// The data used to create the hand
    /// </summary>
    [DataField(required: true)]
    public Hand Data;
}

// <Onyx-FlexibleAnatomy>
[RegisterComponent]
public sealed partial class HandOrganOwnershipComponent : Component
{
    [DataField]
    public Dictionary<EntityUid, string> Hands = new();
}
// </Onyx-FlexibleAnatomy>

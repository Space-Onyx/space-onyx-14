using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Buckle.Components;

public sealed partial class StrapComponent
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, Vector2> BuckleOffsets = new();

    /// <summary>
    /// Whether being buckled to this strap blocks movement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlockMovement = true;

    /// <summary>
    /// Whether the strap shows verbs for buckling entities to it.
    /// </summary>
    [DataField]
    public bool AddBuckleverb = true;

    /// <summary>
    /// Whether entities other than the buckled one may unbuckle it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowOthersToUnbuckle = true;
}

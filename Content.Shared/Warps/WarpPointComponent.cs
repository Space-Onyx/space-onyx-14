using Robust.Shared.GameStates;

namespace Content.Shared.Warps;

/// <summary>
/// Allows ghosts etc to warp to this entity by name.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WarpPointComponent : Component
{
    // <Onyx-WarpLadders>
    /// <summary>
    /// Unique identifier used by interactive warpers to find this point across loaded maps.
    /// </summary>
    [DataField]
    public string Id = string.Empty;
    // </Onyx-WarpLadders>

    [DataField]
    public LocId? Location;

    /// <summary>
    /// If true, ghosts warping to this entity will begin following it.
    /// </summary>
    [DataField]
    public bool Follow;
}

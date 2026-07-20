namespace Content.Shared._Onyx.Gravity.Components;

/// <summary>
/// Applies gravity defaults to every grid currently on this map and to grids
/// that enter it later. The map itself remains unaffected.
/// </summary>
[RegisterComponent]
public sealed partial class GridGravityDefaultsComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public bool Inherent = true;
}

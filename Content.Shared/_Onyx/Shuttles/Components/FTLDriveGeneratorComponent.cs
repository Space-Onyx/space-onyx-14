namespace Content.Shared._Onyx.Shuttles.Components;

/// <summary>
/// Supplies an FTL profile to the shuttle grid while the machine is ready.
/// </summary>
[RegisterComponent]
public sealed partial class FTLDriveGeneratorComponent : Component
{
    [ViewVariables]
    public bool Ready;

    [DataField]
    public int Priority;

    [DataField]
    public FTLDriveData Data;
}

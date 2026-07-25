namespace Content.Shared._Onyx.Xenobiology.Equipment.Components;

[RegisterComponent]
public sealed partial class XenovacTankComponent : Component
{
    [DataField]
    public string ContainerId = "xenovac-storage";

    [DataField]
    public int Capacity = 5;
}

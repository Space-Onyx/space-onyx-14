namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Mechanical wear used by telecommunications buses and servers.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomWearComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Condition = 100f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WearPerHour = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DamageLossMultiplier = 0.1f;
}

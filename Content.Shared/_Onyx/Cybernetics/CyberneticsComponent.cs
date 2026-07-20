using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Cybernetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Disabled;

    [DataField]
    public CyberneticEffect Effects;
}

[Flags]
public enum CyberneticEffect : byte
{
    None = 0,
    MedicalHud = 1 << 0,
    SecurityHud = 1 << 1,
    DiagnosticHud = 1 << 2,
    Prying = 1 << 3,
    Speed = 1 << 4,
}

[RegisterComponent]
public sealed partial class CyberneticBodyEffectsComponent : Component
{
    public bool OwnsPrying;
    public bool OwnsHealthBars;
    public bool OwnsHealthIcons;
    public bool OwnsJobIcons;
    public bool OwnsMindShieldIcons;
    public bool OwnsCriminalRecordIcons;
    public int SpeedLegs;
}

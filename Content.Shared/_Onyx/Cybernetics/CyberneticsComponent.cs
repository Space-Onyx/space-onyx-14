using Robust.Shared.GameStates;
using Content.Shared.Overlays;
using Content.Shared._Onyx.Overlays;

namespace Content.Shared._Onyx.Cybernetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Disabled;

    [DataField]
    public CyberneticEffect Effects;

    [DataField, AutoNetworkedField]
    public bool NightVisionEnabled;

    [DataField, AutoNetworkedField]
    public bool ThermalVisionEnabled;
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
    FlashProtection = 1 << 5,
    NightVision = 1 << 6,
    ThermalVision = 1 << 7,
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
    public bool OwnsSquadIcons;
    public bool OwnsContrabandDetails;
    public bool OwnsAccessReaderSettings;
    public bool OwnsFlashImmunity;
    public bool OwnsNightVision;
    public bool OwnsThermalVision;
    public int SpeedLegs;
}

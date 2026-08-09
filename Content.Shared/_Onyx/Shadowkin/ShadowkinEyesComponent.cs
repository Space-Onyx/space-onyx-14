using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Shadowkin;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShadowkinEyesComponent : Component
{
    [DataField]
    public bool GrantedNightVision;

    [DataField]
    public bool GrantedFlashVulnerability;
}

[RegisterComponent]
public sealed partial class ShadowkinFlashVulnerableComponent : Component;

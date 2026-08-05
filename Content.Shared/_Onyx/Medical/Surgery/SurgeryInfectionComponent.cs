using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryInfectionProtectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ChanceMultiplier = 1f;
}

[RegisterComponent]
public sealed partial class SurgicalSiteInfectionComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryInfectionCooldownComponent : Component
{
    [ViewVariables]
    public TimeSpan NextAttempt;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Swimming.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OceanMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SwimSpeed = 2.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan StrokeInterval = TimeSpan.FromSeconds(0.75);

    [DataField, AutoNetworkedField]
    public TimeSpan StrokeDuration = TimeSpan.FromSeconds(0.25);

    [DataField, AutoNetworkedField]
    public float StrokeAcceleration = 18f;

    [DataField, AutoNetworkedField]
    public float WaterDrag = 4f;

    [DataField, AutoNetworkedField]
    public float StaminaCost = 6f;

    [DataField, AutoNetworkedField]
    public float StaminaRecovery = 2f;

    [DataField, AutoNetworkedField]
    public float MinimumStaminaToSwim = 1f;

    [DataField, AutoNetworkedField]
    public float StaminaToResumeSwimming = 20f;

    [DataField, AutoNetworkedField]
    public float SprintSpeedMultiplier = 1.4f;

    [DataField, AutoNetworkedField]
    public float SprintStaminaCostMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public float DrowningStaminaThreshold = 0.1f;

    [DataField, AutoNetworkedField]
    public float DrowningDamage = 2f;

    [DataField, AutoNetworkedField]
    public float SubmersionDepth = 0.4f;

    [DataField, AutoNetworkedField]
    public float SubmergedAlpha = 0.2f;
}

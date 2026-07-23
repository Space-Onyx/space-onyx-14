using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Overlays;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ThermalVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public float PulseTime;

    [DataField, AutoNetworkedField]
    public float PulseRemaining;

    [DataField, AutoNetworkedField]
    public float LightRadius = 2f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#d06764");

    [DataField, AutoNetworkedField]
    public string ThermalShader = "OnyxThermalVision";

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleThermalVision";

    [DataField]
    public EntityUid? ToggleActionEntity;
}

public sealed partial class ToggleThermalVisionEvent : InstantActionEvent;

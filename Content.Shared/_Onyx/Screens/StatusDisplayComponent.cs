using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Screens;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class StatusDisplayComponent : Component
{
    [DataField, AutoNetworkedField]
    public StatusDisplayContent Content = StatusDisplayContent.Text;

    [DataField, AutoNetworkedField]
    public bool ShowAlertBorder;

    [DataField, AutoNetworkedField]
    public string AlertLevel = string.Empty;

    [DataField, AutoNetworkedField]
    public string Line1 = string.Empty;

    [DataField, AutoNetworkedField]
    public string Line2 = string.Empty;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan TargetTime;

    [DataField, AutoNetworkedField]
    public bool IsAtDestination;
}

[Serializable, NetSerializable]
public enum StatusDisplayVisuals : byte
{
    Content,
    ShowAlertBorder,
    AlertLevel,
}

[Serializable, NetSerializable]
public enum StatusDisplayContent : byte
{
    Text,
    CurrentTime,
    EstimatedTimeOfArrival,
    AlertLevel,
}

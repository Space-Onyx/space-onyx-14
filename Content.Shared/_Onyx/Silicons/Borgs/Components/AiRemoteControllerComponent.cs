using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Silicons.Borgs.Components;

[RegisterComponent]
public sealed partial class AiRemoteControllerComponent : Component
{
    [DataField] public EntityUid? AiHolder;
    [DataField] public EntityUid? LinkedMind;
    [DataField] public string[]? PreviouslyTransmitterChannels;
    [DataField] public string[]? PreviouslyActiveRadioChannels;
    [DataField] public EntProtoId BackToAiAction = "ActionBackToAi";
    [DataField] public EntityUid? BackToAiActionEntity;

    [Serializable, NetSerializable]
    public sealed class RemoteDeviceActionMessage(RemoteDeviceActionEvent remoteAction) : BoundUserInterfaceMessage
    {
        public readonly RemoteDeviceActionEvent RemoteAction = remoteAction;
    }
}

[Serializable, NetSerializable]
public sealed class RemoteDeviceActionEvent(RemoteDeviceActionType actionType, NetEntity target) : EntityEventArgs
{
    public RemoteDeviceActionType ActionType = actionType;
    public NetEntity Target = target;
}

public enum RemoteDeviceActionType
{
    MoveToDevice,
    TakeControl
}

[Serializable, NetSerializable]
public record struct RemoteDevicesData(string DisplayName, NetEntity NetEntityUid);

[Serializable, NetSerializable]
public sealed class RemoteDevicesBuiState(List<RemoteDevicesData> deviceList) : BoundUserInterfaceState
{
    public List<RemoteDevicesData> DeviceList = deviceList;
}

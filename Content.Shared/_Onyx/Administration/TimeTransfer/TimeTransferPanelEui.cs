using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Administration.TimeTransfer;

[Serializable, NetSerializable]
public sealed class TimeTransferPanelEuiState(bool hasFlag) : EuiStateBase
{
    public bool HasFlag { get; } = hasFlag;
}

[Serializable, NetSerializable]
public sealed class TimeTransferEuiMessage(string playerId, List<TimeTransferData> timeData, bool overwrite) : EuiMessageBase
{
    public string PlayerId { get; } = playerId;
    public List<TimeTransferData> TimeData { get; } = timeData;
    public bool Overwrite { get; } = overwrite;
}

[Serializable, NetSerializable]
public sealed class TimeTransferWarningEuiMessage(string message, Color color) : EuiMessageBase
{
    public string Message { get; } = message;
    public Color WarningColor { get; } = color;
}

[DataDefinition]
[Serializable, NetSerializable]
public partial record struct TimeTransferData
{
    [DataField] public string TimeString { get; init; }
    [DataField] public string PlaytimeTracker { get; init; }

    public TimeTransferData(string tracker, string timeString)
    {
        PlaytimeTracker = tracker;
        TimeString = timeString;
    }
}

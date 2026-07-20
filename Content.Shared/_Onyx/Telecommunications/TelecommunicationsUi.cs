using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Telecommunications;

[Serializable, NetSerializable]
public enum TelecomSignalStatus : byte
{
    Routed,
    Garbled,
    RoutingDisabled,
    NoRoute,
    NoReceiver,
    NoBus,
    NoProcessor,
    NoBroadcaster,
    ChannelDisabled,
    Degraded,
    Congested,
    SignalLoss,
    ReceptionLoss,
    BusFault,
    ServerChannelError,
    BroadcastLoss,
}

[Serializable, NetSerializable]
public enum TelecomTrafficConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum TelecomHardwareType : byte
{
    Receiver,
    Processor,
    Bus,
    Broadcaster,
    Server,
}

[Serializable, NetSerializable]
public sealed class TelecomHardwareInfo(
    TelecomHardwareType type,
    int index,
    bool powered,
    int calibration,
    int wear,
    int loadPercent)
{
    public readonly TelecomHardwareType Type = type;
    public readonly int Index = index;
    public readonly bool Powered = powered;
    public readonly int Calibration = calibration;
    public readonly int Wear = wear;
    public readonly int LoadPercent = loadPercent;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficServerInfo(NetEntity entity, string name, bool routingEnabled)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly bool RoutingEnabled = routingEnabled;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficChannelInfo(string id, string name, bool enabled)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficLogInfo(
    string timestamp,
    string channel,
    string message,
    TelecomSignalStatus status,
    int messageLength,
    double timestampSeconds,
    int signalQuality,
    int loadPercent,
    int latencyMilliseconds)
{
    public readonly string Timestamp = timestamp;
    public readonly string Channel = channel;
    public readonly string Message = message;
    public readonly TelecomSignalStatus Status = status;
    public readonly int MessageLength = messageLength;
    public readonly double TimestampSeconds = timestampSeconds;
    public readonly int SignalQuality = signalQuality;
    public readonly int LoadPercent = loadPercent;
    public readonly int LatencyMilliseconds = latencyMilliseconds;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficConsoleState(
    List<TelecomTrafficServerInfo> servers,
    NetEntity? selectedServer,
    bool routingEnabled,
    List<TelecomTrafficChannelInfo> selectedChannels,
    List<TelecomTrafficLogInfo> logs,
    int trafficBins,
    int trafficBinSeconds,
    double currentTimeSeconds,
    int estimatedCalibration,
    int estimatedLoad,
    List<TelecomHardwareInfo> hardware) : BoundUserInterfaceState
{
    public readonly List<TelecomTrafficServerInfo> Servers = servers;
    public readonly NetEntity? SelectedServer = selectedServer;
    public readonly bool RoutingEnabled = routingEnabled;
    public readonly List<TelecomTrafficChannelInfo> SelectedChannels = selectedChannels;
    public readonly List<TelecomTrafficLogInfo> Logs = logs;
    public readonly int TrafficBins = trafficBins;
    public readonly int TrafficBinSeconds = trafficBinSeconds;
    public readonly double CurrentTimeSeconds = currentTimeSeconds;
    public readonly int EstimatedCalibration = estimatedCalibration;
    public readonly int EstimatedLoad = estimatedLoad;
    public readonly List<TelecomHardwareInfo> Hardware = hardware;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficTelemetryMessage(
    int estimatedQuality,
    int estimatedLoad,
    List<TelecomHardwareInfo> hardware) : BoundUserInterfaceMessage
{
    public readonly int EstimatedQuality = estimatedQuality;
    public readonly int EstimatedLoad = estimatedLoad;
    public readonly List<TelecomHardwareInfo> Hardware = hardware;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficSelectServerMessage(NetEntity server) : BoundUserInterfaceMessage
{
    public readonly NetEntity Server = server;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficToggleRoutingMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class TelecomTrafficToggleChannelMessage(string channel) : BoundUserInterfaceMessage
{
    public readonly string Channel = channel;
}

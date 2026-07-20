using Content.Shared._Onyx.Telecommunications;

namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Stores a bounded history of radio traffic routed by a telecommunications server.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomSignalLogComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxEntries = 100;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RoutingEnabled = true;

    [ViewVariables]
    public readonly List<TelecomSignalLogEntry> Entries = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public int Revision;
}

public sealed record TelecomSignalLogEntry(
    TimeSpan Timestamp,
    string Channel,
    string Message,
    TelecomSignalStatus Status,
    int MessageLength,
    int SignalQuality,
    int LoadPercent,
    int LatencyMilliseconds);

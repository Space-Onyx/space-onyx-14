using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Telecommunications;

[Serializable, NetSerializable]
public sealed partial class TelecomCalibrationFinishedEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TelecomMaintenanceFinishedEvent : SimpleDoAfterEvent;

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Gambling.ClawMachine;

[Serializable, NetSerializable]
public sealed partial class ClawMachineDoAfterEvent : SimpleDoAfterEvent;
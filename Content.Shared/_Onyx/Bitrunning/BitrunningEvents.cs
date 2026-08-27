using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Bitrunning;

[ByRefEvent]
public record struct BitrunningGetAntagSelectionBlockerEvent(bool Blocked = false);

public sealed partial class BitrunningDisconnectAvatarActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class BitrunningDisconnectAvatarDoAfterEvent : SimpleDoAfterEvent;

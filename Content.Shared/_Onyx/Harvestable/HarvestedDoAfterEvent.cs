using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Harvestable;

[Serializable, NetSerializable]
public sealed partial class HarvestedDoAfterEvent : SimpleDoAfterEvent;

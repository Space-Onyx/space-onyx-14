using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Item.PseudoItem;

[Serializable, NetSerializable]
public sealed partial class PseudoItemInsertDoAfterEvent : SimpleDoAfterEvent;

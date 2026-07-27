using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Virology;

[Serializable, NetSerializable]
public enum DiseasePenVisuals : byte
{
    Used,
}

[Serializable, NetSerializable]
public sealed partial class DiseasePenInjectEvent : SimpleDoAfterEvent;

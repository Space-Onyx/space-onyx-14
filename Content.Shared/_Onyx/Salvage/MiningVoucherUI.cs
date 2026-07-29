using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Salvage;

[Serializable, NetSerializable]
public sealed class MiningVoucherSelectMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public enum MiningVoucherUiKey : byte
{
    Key
}

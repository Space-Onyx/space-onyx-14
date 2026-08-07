using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Clothing.Modsuits;

[Serializable, NetSerializable]
public enum ModSuitUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ModSuitEjectBatteryBuiMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ModSuitRemoveModuleBuiMessage(NetEntity module) : BoundUserInterfaceMessage
{
    public NetEntity Module = module;
}

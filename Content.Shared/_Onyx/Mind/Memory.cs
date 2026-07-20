using Robust.Shared.Serialization;

namespace Content.Shared.Mind;

[Serializable, NetSerializable]
public sealed class Memory
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string Name { get; set; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public string Value { get; set; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public NetEntity? EntityId { get; set; }

    public Memory()
    {
    }

    public Memory(string name, string value, NetEntity? entityId = null)
    {
        Name = name;
        Value = value;
        EntityId = entityId;
    }
}

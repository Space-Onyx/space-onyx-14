using Content.Shared.Atmos.Components; // <Onyx-RPDPipeLayers>
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RCD;

[Serializable, NetSerializable]
public sealed class RCDSystemMessage(ProtoId<RCDPrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<RCDPrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class RCDConstructionGhostRotationEvent(NetEntity netEntity, Direction direction) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly Direction Direction = direction;
}

// <Onyx-RPD>
[Serializable, NetSerializable]
public sealed class RCDConstructionGhostFlipEvent(NetEntity netEntity, bool useMirrorPrototype) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly bool UseMirrorPrototype = useMirrorPrototype;
}

[Serializable, NetSerializable]
public sealed class RCDConstructionGhostPipeLayerEvent(NetEntity netEntity, AtmosPipeLayer layer) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly AtmosPipeLayer Layer = layer;
}
// </Onyx-RPD>

[Serializable, NetSerializable]
public enum RcdUiKey : byte
{
    Key
}

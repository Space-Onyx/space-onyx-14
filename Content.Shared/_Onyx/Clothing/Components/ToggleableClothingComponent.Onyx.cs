using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing.Components;

public sealed partial class ToggleableClothingComponent
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, EntProtoId> ClothingPrototypes = new();

    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, string> ClothingUids = new();

    [DataField, AutoNetworkedField]
    public bool BlockUnequipWhenAttached;

    [DataField, AutoNetworkedField]
    public bool ReplaceCurrentClothing;
}

[Serializable, NetSerializable]
public enum ToggleClothingUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ToggleableClothingUiMessage(NetEntity attachedClothingUid) : BoundUserInterfaceMessage
{
    public NetEntity AttachedClothingUid = attachedClothingUid;
}

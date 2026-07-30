using Content.Server._Onyx.Botany.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Botany;

[RegisterComponent, Access(typeof(LogSystem))]
public sealed partial class LogComponent : Component
{
    [DataField]
    public EntProtoId SpawnedPrototype = "MaterialWoodPlank1";

    [DataField]
    public int SpawnCount = 2;
}

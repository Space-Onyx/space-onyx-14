using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Onyx.FireControl;

[RegisterComponent]
public sealed partial class FireControlServerComponent : Component
{
    [ViewVariables] public EntityUid? ConnectedGrid;
    [ViewVariables] public readonly HashSet<EntityUid> Controlled = new();
    [ViewVariables] public readonly HashSet<EntityUid> Consoles = new();
    [DataField] public int ProcessingPower;
    [ViewVariables] public int UsedProcessingPower;
}

[RegisterComponent]
public sealed partial class FireControllableComponent : Component
{
    [ViewVariables] public EntityUid? ControllingServer;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextFire;
    [DataField] public float FireCooldown = 0.2f;
}

[RegisterComponent]
public sealed partial class FireControlGridComponent : Component
{
    [ViewVariables] public EntityUid? ControllingServer;
}

[RegisterComponent]
public sealed partial class FireControlPvsComponent : Component
{
    public readonly Dictionary<EntityUid, HashSet<EntityUid>> Overrides = new();
}

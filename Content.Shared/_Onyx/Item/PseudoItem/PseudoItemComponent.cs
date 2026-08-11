using Content.Shared.Item;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Item.PseudoItem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedPseudoItemSystem))]
public sealed partial class PseudoItemComponent : Component
{
    [DataField("size")]
    public ProtoId<ItemSizePrototype> Size = "Huge";

    [DataField, AutoNetworkedField]
    public List<Box2i>? Shape;

    [DataField, AutoNetworkedField]
    public Vector2i StoredOffset;

    [AutoNetworkedField]
    public bool Active;

    [DataField]
    public EntityUid? SleepAction;
}

[RegisterComponent]
public sealed partial class AllowsSleepInsideComponent : Component;

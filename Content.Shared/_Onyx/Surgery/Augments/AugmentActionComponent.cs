using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentActionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentActivatableUIComponent : Component
{
    [DataField(required: true, customTypeSerializer: typeof(EnumSerializer))]
    public Enum? Key;
}

public sealed partial class AugmentActionEvent : InstantActionEvent;

public sealed partial class AugmentToggleActionEvent : InstantActionEvent;

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberDeckComponent : Component
{
    [DataField(required: true)]
    public EntProtoId OpenAction;

    [AutoNetworkedField]
    public EntityUid? OpenActionEntity;

    public EntityUid? GrantedBody;
}

public sealed partial class CyberDeckOpenActionEvent : InstantActionEvent;

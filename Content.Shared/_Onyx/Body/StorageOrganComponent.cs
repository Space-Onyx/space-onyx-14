using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageOrganComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionOpenStorageOrgan";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionOwner;
}

public sealed partial class OpenStorageOrganEvent : InstantActionEvent;

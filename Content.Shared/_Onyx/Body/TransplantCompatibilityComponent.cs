using Content.Shared._Onyx.Body.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransplantCompatibilityComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<TransplantCompatibilityPrototype> Profile;
}

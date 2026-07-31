using Content.Shared._Onyx.Xenomorphs.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Xenomorphs.Larva;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class XenomorphLarvaVictimComponent : Component
{
    [AutoNetworkedField, ViewVariables]
    public ProtoId<InfectionIconPrototype>? InfectedIcon;
}

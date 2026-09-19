// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Hailer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HailerComponent : Component
{
    /// <summary>
    /// Action granted while the hailer mask is equipped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId HailerAction = "ActionHailer";

    /// <summary>
    /// Granted action entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HailActionEntity;
}

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProjectileRequireWhitelistComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public bool RequireClumsy;

    [DataField, AutoNetworkedField]
    public bool CollideWithWalls = true;

    [DataField, AutoNetworkedField]
    public bool Invert;
}

using Content.Shared._Onyx.Shuttles.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Shuttles.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedDockingConsoleSystem))]
[AutoGenerateComponentState]
public sealed partial class DockingConsoleComponent : Component
{
    [DataField(required: true)]
    public LocId WindowTitle;

    [DataField(required: true)]
    public EntityWhitelist ShuttleWhitelist = new();

    [DataField]
    public EntityUid? Shuttle;

    [DataField, AutoNetworkedField]
    public bool HasShuttle;
}

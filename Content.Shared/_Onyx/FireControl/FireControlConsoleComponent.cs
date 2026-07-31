namespace Content.Shared._Onyx.FireControl;

[RegisterComponent]
public sealed partial class FireControlConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedServer;
}

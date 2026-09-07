using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroInterfaceComponent : Component
{
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextUpdate;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroInterfaceConsumerComponent : Component;

[ByRefEvent]
public readonly record struct NeuroInterfaceEnabledChangedEvent(bool Enabled);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NeuroInterfaceRuntimeComponent : Component
{
    [AutoNetworkedField]
    public bool ManuallyEnabled = true;
}

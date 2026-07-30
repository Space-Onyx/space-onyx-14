using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Trigger;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerCounterComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Count;
}

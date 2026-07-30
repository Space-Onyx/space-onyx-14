using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Trigger;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerCounterLimitComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxCount = 1;
}

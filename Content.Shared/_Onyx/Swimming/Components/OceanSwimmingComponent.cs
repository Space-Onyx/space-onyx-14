using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Swimming.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OceanSwimmingComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TimeSpan NextStroke;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan StrokeUntil;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan NextDrowningDamage;

    [ViewVariables, AutoNetworkedField]
    public bool StaminaExhausted;
}

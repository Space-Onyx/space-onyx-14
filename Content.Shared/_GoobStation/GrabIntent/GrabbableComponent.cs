using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.GrabIntent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GrabbableComponent : Component
{
    [DataField, AutoNetworkedField]
    public GrabStage GrabStage;

    [AutoNetworkedField]
    public TimeSpan NextEscapeAttempt;

    [DataField]
    public TimeSpan EscapeAttemptCooldown = TimeSpan.FromSeconds(2);
}

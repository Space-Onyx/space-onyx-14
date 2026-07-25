using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.TimedDespawn;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FadingTimedDespawnComponent : Component
{
    [DataField]
    public float Lifetime = 5f;

    [DataField, AutoNetworkedField]
    public float FadeOutTime = 1f;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool FadeOutStarted;

    public const string AnimationKey = "fadeout";
}

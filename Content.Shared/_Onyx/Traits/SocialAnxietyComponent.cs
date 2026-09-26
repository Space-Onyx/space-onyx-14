using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class SocialAnxietyComponent : Component
{
    [DataField]
    public float DownedTime = 3;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentStrengthComponent : Component
{
    [DataField]
    public float Modifier = 1.25f;
}

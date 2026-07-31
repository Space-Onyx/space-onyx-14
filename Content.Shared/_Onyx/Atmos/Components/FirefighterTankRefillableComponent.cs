using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Atmos.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class FirefighterTankRefillableComponent : Component
{
    [DataField]
    public string SolutionName = "tank";

    [DataField]
    public SoundSpecifier RefillSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");
}

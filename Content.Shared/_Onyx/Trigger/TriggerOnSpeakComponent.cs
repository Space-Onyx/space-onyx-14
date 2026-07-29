using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Trigger;

[RegisterComponent, NetworkedComponent]
public sealed partial class TriggerOnSpeakComponent : Component
{
    [DataField]
    public float ListenRange = 4f;
}

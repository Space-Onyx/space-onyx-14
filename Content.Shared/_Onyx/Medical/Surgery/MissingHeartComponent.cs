using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class MissingHeartComponent : Component
{
    [DataField] public float Progress;
    [DataField] public float NormalDuration;
    [DataField] public float StasisDuration;
}

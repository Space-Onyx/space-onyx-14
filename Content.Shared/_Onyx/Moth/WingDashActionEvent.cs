using Content.Shared.Actions;

namespace Content.Shared._Onyx.Moth;

public sealed partial class WingDashActionEvent : WorldTargetActionEvent
{
    [DataField]
    public float Distance = 4.65f;

    [DataField]
    public float Speed = 9.65f;

    [DataField]
    public float StaminaDrain = 30f;
}

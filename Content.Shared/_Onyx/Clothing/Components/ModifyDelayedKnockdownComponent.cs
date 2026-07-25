namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent]
public sealed partial class ModifyDelayedKnockdownComponent : Component
{
    [DataField] public bool Cancel;
    [DataField] public float DelayDelta;
    [DataField] public float KnockdownTimeDelta;
}

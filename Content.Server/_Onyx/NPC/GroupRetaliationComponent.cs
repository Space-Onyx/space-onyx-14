namespace Content.Server._Onyx.NPC;

[RegisterComponent]
[Access(typeof(GroupRetaliationSystem))]
public sealed partial class GroupRetaliationComponent : Component
{
    [DataField]
    public float Range = 10f;
}

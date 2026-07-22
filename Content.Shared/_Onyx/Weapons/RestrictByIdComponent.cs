namespace Content.Shared._Onyx.Weapons;

[RegisterComponent]
public sealed partial class RestrictByIdComponent : Component
{
    [DataField]
    public bool RestrictMelee = true;

    [DataField]
    public bool RestrictRanged = true;

    [DataField]
    public bool IsEmaggable;

    [DataField]
    public LocId FailText = "restricted-by-id-component-attack-fail-id-wrong";
}

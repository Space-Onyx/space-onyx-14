namespace Content.Shared._Onyx.Weapons;

[RegisterComponent]
public sealed partial class BlurOnCollideComponent : Component
{
    [DataField]
    public TimeSpan BlurTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan BlindTime;
}

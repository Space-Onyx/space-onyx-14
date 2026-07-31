namespace Content.Shared._Onyx.Weapons.Ranged;

[RegisterComponent]
public sealed partial class SyringeGunComponent : Component
{
    [DataField]
    public float InjectionSpeedMultiplier = 1f;
}

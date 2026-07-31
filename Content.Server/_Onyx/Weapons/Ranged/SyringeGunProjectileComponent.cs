namespace Content.Server._Onyx.Weapons.Ranged;

[RegisterComponent]
public sealed partial class SyringeGunProjectileComponent : Component
{
    [DataField]
    public TimeSpan OriginalUpdateInterval;
}

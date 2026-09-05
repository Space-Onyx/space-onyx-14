namespace Content.Shared.Weapons.Melee;

[RegisterComponent]
public sealed partial class ShovingComponent : Component
{
    public const float DefaultStaminaDamage = 14f;

    [DataField]
    public float StaminaDamage = DefaultStaminaDamage;
}

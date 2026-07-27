namespace Content.Shared._Onyx.Disease.Chemistry;

[RegisterComponent]
public sealed partial class ImmunityModifierMetabolismComponent : Component
{
    [DataField] public float GainRateModifier;
    [DataField] public float StrengthModifier;
    [DataField] public TimeSpan ModifierTimer;
}

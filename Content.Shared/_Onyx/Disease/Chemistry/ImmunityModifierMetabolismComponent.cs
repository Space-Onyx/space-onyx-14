using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Disease.Chemistry;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImmunityModifierMetabolismComponent : Component
{
    [DataField, AutoNetworkedField] public float GainRateModifier;
    [DataField, AutoNetworkedField] public float StrengthModifier;
    [DataField, AutoNetworkedField] public TimeSpan ModifierTimer;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Stealth.ForcedStealth;

[RegisterComponent, NetworkedComponent]
public sealed partial class ForcedStealthStatusEffectComponent : Component
{
    [DataField]
    public float Visibility;
}

[RegisterComponent]
public sealed partial class ForcedStealthStateComponent : Component
{
    public bool AddedStealth;

    public bool PreviousEnabled;

    public float PreviousVisibility;

    public List<EntityUid> ActiveOverrides = [];
}

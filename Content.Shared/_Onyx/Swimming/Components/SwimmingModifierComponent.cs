using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Swimming.Components;

/// <summary>
/// Per-entity modifiers applied on top of an ocean map's swimming settings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SwimmingModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float StaminaCostMultiplier = 1f;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Bosses.Anger.Components;

/// <summary>
/// Makes action's delay depend on current anger level of the parent entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AngerDelayActionComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MinDelay;

    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MaxDelay;

    [DataField, AutoNetworkedField]
    public bool Inverse;
}

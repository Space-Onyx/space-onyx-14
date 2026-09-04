using Robust.Shared.GameStates;

namespace Content.Shared.Genetics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticsSmallHeightComponent : Component
{
    [AutoNetworkedField]
    public float? PreviousHeight;
}

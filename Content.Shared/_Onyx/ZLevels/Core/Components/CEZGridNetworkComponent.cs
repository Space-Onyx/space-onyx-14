using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.ZLevels.Core.Components;

/// <summary>
/// Runtime manager reconstructed from active <see cref="CEZGridConnectorComponent"/> entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CEZGridNetworkComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string NetworkId = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> Grids = new();

    [ViewVariables]
    public EntityUid AnchorGrid = EntityUid.Invalid;

    [ViewVariables]
    public float TotalCachedMass;

    [ViewVariables]
    public bool HasStaticAnchor;
}

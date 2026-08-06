using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.ZLevels.Core.Components;

/// <summary>
/// Runtime membership added to grids connected by <see cref="CEZGridConnectorComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CEZGridComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string NetworkId = string.Empty;

    [ViewVariables]
    public EntityUid Network = EntityUid.Invalid;

    [ViewVariables]
    public Vector2 NetworkOffset;

    [ViewVariables]
    public Angle NetworkRotation;

    [ViewVariables]
    public float CachedMass;
}

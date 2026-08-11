using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Bed.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DoubleBedComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 LeftOffset = new(0f, -0.25f);

    [DataField, AutoNetworkedField]
    public Vector2 RightOffset = new(0f, 0.25f);

    [DataField, AutoNetworkedField]
    public Vector2 LeftBedsheetOffset = new(0f, 0.5f);

    [DataField, AutoNetworkedField]
    public Vector2 RightBedsheetOffset = Vector2.Zero;

    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, EntityUid> SleepActions = new();
}

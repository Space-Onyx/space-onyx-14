using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Buckle.Components;

public sealed partial class StrapComponent
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, Vector2> BuckleOffsets = new();
}

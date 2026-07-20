using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

public sealed partial class SunShadowComponent
{
    [DataField, AutoNetworkedField]
    public bool CastRoofShadows;

    [DataField, AutoNetworkedField]
    public float RoofHeight = 1f;
}

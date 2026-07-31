using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.SpaceArtillery.Components;

[RegisterComponent]
public sealed partial class SpaceArtilleryComponent : Component
{
    [DataField]
    public float PowerUsePassive = 600f;

    [DataField]
    public float PowerChargeRate = 3000f;

    [DataField]
    public float PowerUseActive = 6000f;

    [DataField]
    public ProtoId<SinkPortPrototype> SpaceArtilleryFirePort = "SpaceArtilleryFire";
}

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.ObraDinn;

[RegisterComponent, NetworkedComponent]
public sealed partial class ObraDinnHologramComponent : Component
{
    [DataField]
    public string RealName = "unknown";

    [DataField]
    public float ListenRange = 5f;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/hiss.ogg");

    [DataField]
    public EntProtoId SpawnEffect = "PuddleSparkle";
}

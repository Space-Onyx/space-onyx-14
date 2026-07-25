using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenobiology.Extracts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class SlimeExtractComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used;

    [ViewVariables]
    public bool Processing;
}

[Serializable, NetSerializable]
public enum SlimeExtractVisuals : byte
{
    Used,
}

using Content.Shared._Onyx.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Targeting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PartStatusComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<TargetBodyPart, PartStatus> Parts = [];
}

[Serializable, NetSerializable]
public readonly record struct PartStatus(
    bool Exists,
    PartDamageSeverity Severity,
    bool Bleeding,
    FractureGrade Fracture,
    bool Scar);

[Serializable, NetSerializable]
public enum PartDamageSeverity : byte
{
    None,
    Minor,
    Moderate,
    Severe,
    Critical,
}

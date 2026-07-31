using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Bloodtrak;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodtrakComponent : Component
{
    [DataField]
    public TimeSpan MaximumTrackingDuration = TimeSpan.FromMinutes(8);

    [DataField]
    public float MaxDistance = 128f;

    [DataField]
    public float MediumDistance = 16f;

    [DataField]
    public float CloseDistance = 8f;

    [DataField]
    public float ReachedDistance = 1f;

    [DataField]
    public double Precision = 0.09;

    [ViewVariables]
    public EntityUid? Target;

    [AutoNetworkedField]
    public bool IsActive;

    [AutoNetworkedField]
    public Angle ArrowAngle;

    [AutoNetworkedField]
    public BloodtrakDistance DistanceToTarget;

    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan ExpirationTime;

    [ViewVariables]
    public EntityUid? LastScannedTarget;

    [ViewVariables]
    public List<(string Dna, EntityUid Owner)> Results = [];

    [ViewVariables]
    public int ResultOffset;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> FirstScanned = new();
}

[Serializable, NetSerializable]
public enum BloodtrakDistance : byte
{
    Unknown,
    Reached,
    Close,
    Medium,
    Far
}

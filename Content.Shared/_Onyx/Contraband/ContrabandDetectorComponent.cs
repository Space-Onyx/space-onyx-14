using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Contraband;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ContrabandDetectorComponent : Component
{
    [DataField]
    public SoundSpecifier? NoDetect;

    [DataField]
    public SoundSpecifier? Detect;

    [DataField]
    public float FalseDetectingChance = 0.05f;

    [DataField, AutoNetworkedField]
    public bool IsFalseScanning;

    [DataField, AutoNetworkedField]
    public bool IsFalseDetectingChanged;

    [DataField]
    public float FalseDetectingChanceMultiplier = 10f;

    [DataField]
    public Dictionary<EntityUid, TimeSpan> Scanned = new();

    [DataField]
    public TimeSpan ScanTimeOut = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public ContrabandDetectorState State;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan LastScanTime;
}

[Serializable, NetSerializable]
public enum ContrabandDetectorVisuals
{
    VisualState
}

[Serializable, NetSerializable]
public enum ContrabandDetectorState
{
    Off,
    Powered,
    Alarm,
    Scan
}

[Serializable, NetSerializable]
public enum ContrabandDetectorChanceWireKey : byte
{
    StatusKey,
    TimeoutKey
}

[Serializable, NetSerializable]
public enum ContrabandDetectorFakeScanWireKey : byte
{
    StatusKey,
    TimeoutKey
}

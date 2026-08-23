using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Animations;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.AnimationData;

[Prototype, DataDefinition]
public sealed partial class AnimationPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<AnimationTrackData> Tracks = new();
    [DataField(required: true)] public float Length;
}

[Serializable, DataDefinition]
public abstract partial class AnimationTrackData
{
    [DataField, AlwaysPushInheritance] public List<KeyFrameData> KeyFrames = new();
}

[Serializable, DataDefinition]
public sealed partial class AnimationTrackComponentPropertyData : AnimationTrackData
{
    [DataField, AlwaysPushInheritance] public string ComponentType = "";
    [DataField, AlwaysPushInheritance] public string Property = "";
    [DataField, AlwaysPushInheritance] public AnimationInterpolationMode InterpolationMode;
}

[Serializable, DataDefinition]
public sealed partial class AnimationTrackPlaySoundData : AnimationTrackData;

[Serializable, DataDefinition]
public abstract partial class KeyFrameData
{
    [DataField, AlwaysPushInheritance] public float Keyframe;
}

[Serializable, DataDefinition]
public sealed partial class KeyFrameSoundData : KeyFrameData
{
    [DataField, AlwaysPushInheritance] public SoundSpecifier Sound = default!;
}

[Serializable, DataDefinition]
public sealed partial class KeyFrameComponentPropertyData : KeyFrameData
{
    [DataField, AlwaysPushInheritance] public string Value = "";
    [DataField, AlwaysPushInheritance] public string Type = "";
}

[Serializable, NetSerializable]
public sealed class PlayAnimationMessage(NetEntity animatedEntity, string animationId) : EntityEventArgs
{
    public NetEntity AnimatedEntity = animatedEntity;
    public string AnimationID = animationId;
}

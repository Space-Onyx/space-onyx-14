using System.Globalization;
using System.Linq;
using System.IO;
using Content.Shared._Onyx.AnimationData;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.AnimationData;

public sealed partial class PrototypedAnimationPlayerSystem : EntitySystem
{
    [Dependency] private IComponentFactory _components = default!;
    [Dependency] private AnimationPlayerSystem _animations = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private const string Key = "prototyped-animation";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayAnimationMessage>(OnPlay);
    }

    private void OnPlay(PlayAnimationMessage ev)
    {
        PlayAnimation(GetEntity(ev.AnimatedEntity), ev.AnimationID);
    }

    public void PlayAnimation(EntityUid entity, string animationId)
    {
        if (_prototypes.TryIndex<AnimationPrototype>(animationId, out var proto))
            Play(entity, proto);
    }

    private void Play(EntityUid entity, AnimationPrototype proto)
    {
        var key = proto.ID == "EmoteJump" ? $"{Key}-jump" : Key;
        if (!entity.Valid || _animations.HasRunningAnimation(entity, key)) return;
        var animation = new Robust.Client.Animations.Animation { Length = TimeSpan.FromSeconds(proto.Length) };
        foreach (var data in proto.Tracks)
        {
            if (data is AnimationTrackComponentPropertyData componentData)
            {
                if (!_components.TryGetRegistration(componentData.ComponentType, out var registration, true)) return;
                var track = new AnimationTrackComponentProperty { ComponentType = _components.GetComponent(registration.Idx).GetType(), Property = componentData.Property, InterpolationMode = componentData.InterpolationMode };
                foreach (var frame in componentData.KeyFrames.OfType<KeyFrameComponentPropertyData>())
                {
                    object value = frame.Type.ToLowerInvariant() switch
                    {
                        "int" => (object) int.Parse(frame.Value, CultureInfo.InvariantCulture),
                        "float" => (object) float.Parse(frame.Value, CultureInfo.InvariantCulture),
                        "vector2" => (object) YamlHelpers.AsVector2(frame.Value),
                        "angle" => (object) Angle.FromDegrees(float.Parse(frame.Value, CultureInfo.InvariantCulture)),
                        _ => throw new InvalidDataException($"Unknown animation value type {frame.Type}")
                    };
                    track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(value, frame.Keyframe));
                }
                animation.AnimationTracks.Add(track);
            }
            else if (data is AnimationTrackPlaySoundData soundData)
            {
                var track = new AnimationTrackPlaySound();
                foreach (var frame in soundData.KeyFrames.OfType<KeyFrameSoundData>())
                    track.KeyFrames.Add(new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(frame.Sound), frame.Keyframe));
                animation.AnimationTracks.Add(track);
            }
        }
        _animations.Play(entity, animation, key);
    }
}

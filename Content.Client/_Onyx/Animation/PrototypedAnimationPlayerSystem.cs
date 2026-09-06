using System.Globalization;
using System.Linq;
using System.IO;
using System.Numerics;
using Content.Shared._Onyx.AnimationData;
using Content.Shared.Damage.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
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
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    private const string Key = "prototyped-animation";
    private const string JumpKey = Key + "-jump";
    // Mirrors the private key of the vanilla stamina wobble animation.
    private const string StaminaAnimationKey = "stamina";

    private readonly Dictionary<(EntityUid Uid, string Key), SpriteVisualState> _trackedAnimations = new();
    private readonly List<(EntityUid Uid, string Key)> _finishedAnimations = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayAnimationMessage>(OnPlay);
        SubscribeLocalEvent<AnimationCompletedEvent>(OnAnimationCompleted);
    }

    public override void Shutdown()
    {
        foreach (var entry in _trackedAnimations)
            RestoreVisualState(entry.Key.Uid, entry.Value);
        _trackedAnimations.Clear();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_trackedAnimations.Count == 0)
            return;

        _finishedAnimations.Clear();
        foreach (var (uid, key) in _trackedAnimations.Keys)
        {
            if (_animations.HasRunningAnimation(uid, key))
            {
                if (key == JumpKey && _animations.HasRunningAnimation(uid, StaminaAnimationKey))
                    _animations.Stop(uid, StaminaAnimationKey);
                continue;
            }
            _finishedAnimations.Add((uid, key));
        }

        foreach (var (uid, key) in _finishedAnimations)
            FinishAnimation(uid, key, interrupted: true);
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

    private void OnAnimationCompleted(AnimationCompletedEvent args)
    {
        FinishAnimation(args.Uid, args.Key, interrupted: !args.Finished);
    }

    private void FinishAnimation(EntityUid uid, string key, bool interrupted)
    {
        if (_trackedAnimations.Remove((uid, key), out var state) && interrupted)
            RestoreVisualState(uid, state);
    }

    private void Play(EntityUid entity, AnimationPrototype proto)
    {
        var key = proto.ID == "EmoteJump" ? JumpKey : Key;
        if (!entity.Valid || _animations.HasRunningAnimation(entity, key)) return;
        var jumpOffset = Vector2.Zero;
        var jumpRotation = Angle.Zero;
        if (proto.ID == "EmoteJump" &&
            TryComp(entity, out SpriteComponent? sprite) &&
            TryComp(entity, out TransformComponent? xform))
        {
            if (_animations.HasRunningAnimation(entity, StaminaAnimationKey) &&
                TryComp(entity, out StaminaComponent? stamina))
            {
                // A running stamina wobble owns Sprite.Offset too. Yield it for the jump:
                // return to its clean base at once so the arc and the snapshot below agree.
                _animations.Stop(entity, StaminaAnimationKey);
                _sprite.SetOffset((entity, sprite), stamina.StartOffset);
                jumpOffset = stamina.StartOffset;
            }
            else
            {
                jumpOffset = sprite.Offset;
            }
            // NoRotation sprites ignore world/eye rotation in the renderer,
            // so their local offset is already screen-aligned.
            jumpRotation = sprite.NoRotation
                ? Angle.Zero
                : _transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation;
        }
        TrackVisualState(entity, key, proto);
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
                    if (proto.ID == "EmoteJump" && value is Vector2 offset)
                        value = jumpOffset + (-jumpRotation).RotateVec(offset);
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

    private void TrackVisualState(EntityUid entity, string key, AnimationPrototype proto)
    {
        var touchesOffset = false;
        var touchesRotation = false;
        foreach (var data in proto.Tracks)
        {
            if (data is not AnimationTrackComponentPropertyData componentData)
                continue;
            if (!_components.TryGetRegistration(componentData.ComponentType, out var registration, true))
                continue;
            if (_components.GetComponent(registration.Idx).GetType() != typeof(SpriteComponent))
                continue;
            if (componentData.Property == nameof(SpriteComponent.Offset))
                touchesOffset = true;
            else if (componentData.Property == nameof(SpriteComponent.Rotation))
                touchesRotation = true;
        }

        if (!touchesOffset && !touchesRotation)
            return;
        if (!TryComp(entity, out SpriteComponent? sprite))
            return;

        Vector2? offset = null;
        Angle? rotation = null;
        if (touchesOffset)
            offset = FindTrackedOffset(entity) ?? sprite.Offset;
        if (touchesRotation)
            rotation = FindTrackedRotation(entity) ?? sprite.Rotation;
        _trackedAnimations[(entity, key)] = new SpriteVisualState(offset, rotation);
    }

    private Vector2? FindTrackedOffset(EntityUid entity)
    {
        foreach (var entry in _trackedAnimations)
        {
            if (entry.Key.Uid == entity && entry.Value.Offset != null)
                return entry.Value.Offset;
        }

        return null;
    }

    private Angle? FindTrackedRotation(EntityUid entity)
    {
        foreach (var entry in _trackedAnimations)
        {
            if (entry.Key.Uid == entity && entry.Value.Rotation != null)
                return entry.Value.Rotation;
        }

        return null;
    }

    private void RestoreVisualState(EntityUid uid, SpriteVisualState state)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;
        if (state.Offset is { } offset)
            _sprite.SetOffset((uid, sprite), offset);
        if (state.Rotation is { } rotation)
            _sprite.SetRotation((uid, sprite), rotation);
    }

    private sealed record SpriteVisualState(Vector2? Offset, Angle? Rotation);
}

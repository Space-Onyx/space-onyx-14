using System.Linq;
using System.Numerics;
using Content.Client.DamageState;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Emoting;

public sealed partial class AnimatedEmotesSystem : SharedAnimatedEmotesSystem
{
    [Dependency] private AnimationPlayerSystem _animations = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnimatedEmotesComponent, ComponentHandleState>(OnState);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationFlipEmoteEvent>(OnFlip);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationSpinEmoteEvent>(OnSpin);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationJumpEmoteEvent>(OnJump);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationTweakEmoteEvent>(OnTweak);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationFlexEmoteEvent>(OnFlex);
    }

    private void OnFlip(Entity<AnimatedEmotesComponent> ent, ref AnimationFlipEmoteEvent args) =>
        Play(ent, new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromMilliseconds(500),
            AnimationTracks = { Property<SpriteComponent>(nameof(SpriteComponent.Rotation), AnimationInterpolationMode.Linear, new(Angle.Zero, 0), new(Angle.FromDegrees(180), .25f), new(Angle.FromDegrees(360), .25f)) }
        }, "emoteAnimKeyId");

    private void OnSpin(Entity<AnimatedEmotesComponent> ent, ref AnimationSpinEmoteEvent args) =>
        Play(ent, new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromMilliseconds(600),
            AnimationTracks = { Property<TransformComponent>(nameof(TransformComponent.LocalRotation), AnimationInterpolationMode.Linear, new(Angle.Zero, 0), new(Angle.FromDegrees(90), .075f), new(Angle.FromDegrees(180), .075f), new(Angle.FromDegrees(270), .075f), new(Angle.Zero, .075f), new(Angle.FromDegrees(90), .075f), new(Angle.FromDegrees(180), .075f), new(Angle.FromDegrees(270), .075f), new(Angle.Zero, .075f)) }
        }, "emoteAnimSpin");

    private void OnJump(Entity<AnimatedEmotesComponent> ent, ref AnimationJumpEmoteEvent args) =>
        Play(ent, new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromMilliseconds(250),
            AnimationTracks = { Property<SpriteComponent>(nameof(SpriteComponent.Offset), AnimationInterpolationMode.Cubic, new(Vector2.Zero, 0), new(new Vector2(0, .35f), .125f), new(Vector2.Zero, .125f)) }
        }, "emoteAnimKeyId");

    private void OnTweak(Entity<AnimatedEmotesComponent> ent, ref AnimationTweakEmoteEvent _)
    {
        if (!TryComp(ent, out MetaDataComponent? metadata) || metadata.EntityPrototype is not { } prototype)
            return;
        var stateNumber = string.Concat(prototype.ID.Where(char.IsDigit));
        if (stateNumber.Length == 0) stateNumber = "0";
        var track = new AnimationTrackSpriteFlick
        {
            LayerKey = DamageStateVisualLayers.Base,
            KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId($"{prototype.SetName}-tweaking-{stateNumber}"), 0f) }
        };
        Play(ent, new Robust.Client.Animations.Animation { Length = TimeSpan.FromSeconds(1.1), AnimationTracks = { track } }, "emoteAnimTweak");
    }

    private void OnFlex(Entity<AnimatedEmotesComponent> ent, ref AnimationFlexEmoteEvent _)
    {
        if (!TryComp(ent, out MetaDataComponent? metadata) || metadata.EntityPrototype is not { } prototype || prototype.SetName is not { } setName)
            return;
        var name = setName.ToLowerInvariant();
        var animation = new Robust.Client.Animations.Animation { Length = TimeSpan.FromSeconds(1.5) };
        animation.AnimationTracks.Add(new AnimationTrackSpriteFlick
            {
                LayerKey = DamageStateVisualLayers.Base,
                KeyFrames =
                {
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId($"{name}_flex"), 0f),
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId(name), 1.4f)
                }
            });
        animation.AnimationTracks.Add(new AnimationTrackSpriteFlick
            {
                LayerKey = DamageStateVisualLayers.BaseUnshaded,
                KeyFrames =
                {
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId($"{name}_flex_damage"), 0f),
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("nautdamage"), 1.4f)
                }
            });
        Play(ent, animation, "emoteAnimFlex");
    }

    private void OnState(EntityUid uid, AnimatedEmotesComponent _, ref ComponentHandleState args)
    {
        if (args.Current is not AnimatedEmotesComponentState state ||
            !_prototypes.TryIndex<EmotePrototype>(state.Emote, out var emote) ||
            emote.Event is null)
            return;

        RaiseLocalEvent(uid, emote.Event);
    }

    private void Play(Entity<AnimatedEmotesComponent> ent, Robust.Client.Animations.Animation animation, string key) => _animations.Play(ent.Owner, animation, key);
    private static AnimationTrackComponentProperty Property<T>(string property, AnimationInterpolationMode mode, params AnimationTrackProperty.KeyFrame[] frames)
    {
        var track = new AnimationTrackComponentProperty { ComponentType = typeof(T), Property = property, InterpolationMode = mode };
        track.KeyFrames.AddRange(frames);
        return track;
    }
}

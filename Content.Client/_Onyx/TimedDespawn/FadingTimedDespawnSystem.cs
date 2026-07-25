using Content.Shared._Onyx.TimedDespawn;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.TimedDespawn;

public sealed partial class FadingTimedDespawnSystem : SharedFadingTimedDespawnSystem
{
    [Dependency] private AnimationPlayerSystem _animations = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FadingTimedDespawnComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<FadingTimedDespawnComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _animations.Stop(ent.Owner, FadingTimedDespawnComponent.AnimationKey);
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.SetColor((ent, sprite), sprite.Color.WithAlpha(1f));
    }

    protected override void FadeOut(Entity<FadingTimedDespawnComponent> ent)
    {
        if (_animations.HasRunningAnimation(ent, FadingTimedDespawnComponent.AnimationKey) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.FadeOutTime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color, 0f),
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), ent.Comp.FadeOutTime),
                    },
                },
            },
        };
        _animations.Play(ent, animation, FadingTimedDespawnComponent.AnimationKey);
    }

    protected override bool CanDelete(EntityUid uid) => IsClientSide(uid);
}

using Content.Shared._Onyx.Sprinting;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Sprinting;

public sealed partial class SprintingSystem : SharedSprintingSystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Animation StartAnimation = Flick("sprint_cloud", 0.48f);
    private static readonly Animation StepAnimation = Flick("sprint_cloud_small", 0.34f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprinterComponent, SprintStartEvent>(OnSprintStart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SprinterComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.IsSprinting || !_timing.IsFirstTimePredicted ||
                _timing.CurTime - component.LastStep < component.TimeBetweenSteps)
                continue;

            var effect = Spawn(component.StepAnimation, Transform(uid).Coordinates);
            _animation.Play(effect, StepAnimation, "sprint-cloud-small");
            component.LastStep = _timing.CurTime;
        }
    }

    private void OnSprintStart(Entity<SprinterComponent> ent, ref SprintStartEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var effect = Spawn(ent.Comp.SprintAnimation, Transform(ent).Coordinates);
        _animation.Play(effect, StartAnimation, "sprint-cloud");
    }

    private static Animation Flick(string state, float duration) => new()
    {
        Length = TimeSpan.FromSeconds(duration),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = SprintVisualLayers.Base,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId(state), 0f) },
            },
        },
    };
}

public enum SprintVisualLayers : byte
{
    Base,
}

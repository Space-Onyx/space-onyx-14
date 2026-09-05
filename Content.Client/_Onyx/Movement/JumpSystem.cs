// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Client._Onyx.AnimationData;
using Content.Shared._Onyx.Movement;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Movement;

public sealed partial class JumpSystem : SharedJumpSystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PrototypedAnimationPlayerSystem _animation = default!;

    private JumpShadowOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new JumpShadowOverlay(EntityManager, _timing);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<JumpShadowOverlay>();
        base.Shutdown();
    }

    protected override void OnJumpStarted(Entity<JumpComponent> ent) =>
        _animation.PlayAnimation(ent, "EmoteJump");
}

public sealed class JumpShadowOverlay(IEntityManager entities, IGameTiming timing) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = entities.EntityQueryEnumerator<JumpComponent, TransformComponent>();
        while (query.MoveNext(out _, out var jump, out var xform))
        {
            if (!jump.IsJumping)
                continue;

            var duration = Math.Max((jump.JumpEnds - jump.JumpStarted).TotalSeconds, 0.001);
            var progress = Math.Clamp((timing.CurTime - jump.JumpStarted).TotalSeconds / duration, 0, 1);
            var height = MathF.Sin((float) progress * MathF.PI);
            var position = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(position))
                continue;

            args.WorldHandle.SetTransform(Matrix3x2.CreateScale(1f + height * 1.5f, 0.45f + height * 0.3f) *
                                          Matrix3Helpers.CreateTranslation(position));
            args.WorldHandle.DrawCircle(Vector2.Zero, 0.22f, Color.Black.WithAlpha(0.35f - height * 0.15f));
        }

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }
}

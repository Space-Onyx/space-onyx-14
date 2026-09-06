// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Client._Onyx.AnimationData;
using Content.Shared._Onyx.Movement;
using Robust.Client.GameObjects;
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

    private const float HopDuration = 0.5f;

    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly SpriteSystem _sprite = entities.System<SpriteSystem>();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var screenDown = (-eyeRot).RotateVec(new Vector2(0f, -1f));
        var screenTilt = Matrix3Helpers.CreateRotation(-eyeRot);
        var now = timing.CurTime;

        var query = entities.EntityQueryEnumerator<JumpComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var jump, out var sprite, out var xform))
        {
            var elapsed = (now - jump.JumpStarted).TotalSeconds;
            if (elapsed < 0 || elapsed > HopDuration)
                continue;

            var progress = elapsed / HopDuration;
            var height = MathF.Sin((float) progress * MathF.PI);
            var feet = _transform.GetWorldPosition(xform) +
                screenDown * (_sprite.GetLocalBounds((uid, sprite)).Height / 2f);
            if (!args.WorldAABB.Contains(feet))
                continue;

            args.WorldHandle.SetTransform(Matrix3x2.CreateScale(1f + height * 1.5f, 0.45f + height * 0.3f) *
                                          screenTilt *
                                          Matrix3Helpers.CreateTranslation(feet));
            args.WorldHandle.DrawCircle(Vector2.Zero, 0.22f, Color.Black.WithAlpha(0.35f - height * 0.15f));
        }

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }
}

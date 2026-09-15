// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Shared._Onyx.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Overlays;

/// <summary>
///     Fullscreen pass shifting color saturation toward the mood-driven target.
/// </summary>
public sealed partial class SaturationScaleOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly ProtoId<ShaderPrototype> SaturationShaderId = "SaturationScale";

    private readonly ShaderInstance _shader;
    private float _currentSaturation = 1f;

    public SaturationScaleOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototypeManager.Index(SaturationShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.HasComponent<SaturationScaleOverlayComponent>(player))
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null
            || _playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.HasComponent<SaturationScaleOverlayComponent>(player))
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("saturation", _currentSaturation);

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (ScreenTexture is null
            || _playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.TryGetComponent(player, out SaturationScaleOverlayComponent? saturationComp)
            || MathHelper.CloseTo(_currentSaturation, saturationComp.SaturationScale))
            return;

        var fade = Math.Max(saturationComp.FadeInMultiplier, 1e-6f);
        var step = args.DeltaSeconds * fade;
        var target = saturationComp.SaturationScale;
        _currentSaturation = MathHelper.CloseTo(_currentSaturation, target, step)
            ? target
            : MathHelper.Lerp(_currentSaturation, target, Math.Clamp(step, 0f, 1f));
        _shader.SetParameter("saturation", _currentSaturation);
    }
}

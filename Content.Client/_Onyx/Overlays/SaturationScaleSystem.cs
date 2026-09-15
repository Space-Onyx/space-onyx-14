// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared._Onyx.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Onyx.Overlays;

/// <summary>
///     Shows or hides the mood saturation overlay as the local player
///     gains or loses the replicated component, honoring the client CVar.
/// </summary>
public sealed partial class SaturationScaleSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private ISharedPlayerManager _playerMan = default!;
    [Dependency] private IConfigurationManager _cfgMan = default!;

    private SaturationScaleOverlay _overlay = default!;
    private bool _moodEffectsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SaturationScaleOverlay();
        _moodEffectsEnabled = _cfgMan.GetCVar(CCVars.MoodVisualEffects);
        _cfgMan.OnValueChanged(CCVars.MoodVisualEffects, HandleMoodEffectsUpdated);

        SubscribeLocalEvent<SaturationScaleOverlayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SaturationScaleOverlayComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SaturationScaleOverlayComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SaturationScaleOverlayComponent, PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
    }

    public override void Shutdown()
    {
        _cfgMan.UnsubValueChanged(CCVars.MoodVisualEffects, HandleMoodEffectsUpdated);

        base.Shutdown();
    }

    private void HandleMoodEffectsUpdated(bool moodEffectsEnabled)
    {
        if (_overlayMan.HasOverlay<SaturationScaleOverlay>() && !moodEffectsEnabled)
            _overlayMan.RemoveOverlay(_overlay);

        _moodEffectsEnabled = moodEffectsEnabled;
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (!_moodEffectsEnabled)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, SaturationScaleOverlayComponent component, PlayerDetachedEvent args)
    {
        if (!_moodEffectsEnabled)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, SaturationScaleOverlayComponent component, PlayerAttachedEvent args)
    {
        if (!_moodEffectsEnabled)
            return;

        if (!_overlayMan.HasOverlay<SaturationScaleOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, SaturationScaleOverlayComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity || !_moodEffectsEnabled)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(EntityUid uid, SaturationScaleOverlayComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity || !_moodEffectsEnabled)
            return;

        if (!_overlayMan.HasOverlay<SaturationScaleOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }
}

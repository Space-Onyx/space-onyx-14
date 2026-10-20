// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Onyx.Overlays;

public sealed partial class EntityOcclusionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private EntityOcclusionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new EntityOcclusionOverlay();
        SubscribeLocalEvent<SpriteComponent, BeforePostShaderRenderEvent>(OnShaderRender);
        Subs.CVar(_configuration, CCVars.EntityOcclusionEnabled, OnEnabledChanged, true);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        _overlay.Dispose();
        base.Shutdown();
    }

    private void OnShaderRender(Entity<SpriteComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id == EntityOcclusionOverlay.PostShaderId)
            _overlay.ConfigureShader(ent, args);
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (enabled)
            _overlays.AddOverlay(_overlay);
        else
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay.RestoreAll();
        }
    }
}

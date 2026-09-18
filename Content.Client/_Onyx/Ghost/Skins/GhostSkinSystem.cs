// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Ghost.Skins;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Ghost.Skins;

/// <summary>
/// Applies <see cref="GhostSkinComponent"/> visuals to the observer base layer.
/// Runs on any ghost entity carrying the component, including admin ghosts.
/// </summary>
public sealed partial class GhostSkinSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostSkinComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<GhostSkinComponent> ent, ref ComponentStartup args)
    {
        ApplySkin(ent);
    }

    /// <summary>
    /// Replaces the observer base layer with the skin look. Extra layers stay untouched.
    /// </summary>
    public void ApplySkin(Entity<GhostSkinComponent> ent)
    {
        if (!_proto.TryIndex(ent.Comp.Skin, out var skin))
            return;

        if (skin.Sprite is not { } rsi || skin.State is not { } state)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var target = (ent.Owner, sprite);
        if (!_sprite.LayerExists(target, 0))
            return;

        _sprite.LayerSetRsi(target, 0, rsi, new RSI.StateId(state));
        if (skin.Color is { } color)
            _sprite.LayerSetColor(target, 0, color);

        _sprite.ForceUpdate(ent);
    }
}

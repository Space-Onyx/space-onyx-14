// SPDX-FileCopyrightText: 2025 AftrLite <61218133+AftrLite@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.GameObjects;
using Content.Shared._Onyx.CosmicCult.Components;

namespace Content.Client._Onyx.CosmicCult;

/// <summary>
/// Visualizer for The Monument of the Cosmic Cult.
/// </summary>
public sealed partial class MonumentVisualizerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonumentComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(Entity<MonumentComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (ent.Owner, args.Sprite);
        _sprite.LayerMapTryGet(sprite, MonumentVisualLayers.TransformLayer, out var transformLayer, false);
        _sprite.LayerMapTryGet(sprite, MonumentVisualLayers.MonumentLayer, out var baseLayer, false);
        _appearance.TryGetData<bool>(ent, MonumentVisuals.Transforming, out var transforming, args.Component);
        _appearance.TryGetData<bool>(ent, MonumentVisuals.Tier3, out var tier3, args.Component);

        if (!tier3)
            _sprite.LayerSetRsiState(sprite, transformLayer, "transform-stage2");
        else
            _sprite.LayerSetRsiState(sprite, transformLayer, "transform-stage3");

        if (transforming && HasComp<MonumentTransformingComponent>(ent))
        {
            _sprite.LayerSetAnimationTime(sprite, transformLayer, 0f);
            _sprite.LayerSetVisible(sprite, transformLayer, true);
            _sprite.LayerSetVisible(sprite, baseLayer, false);
        }
        else
        {
            _sprite.LayerSetVisible(sprite, transformLayer, false);
            _sprite.LayerSetVisible(sprite, baseLayer, true);
        }
    }
}

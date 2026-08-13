// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.DamageState;
using Content.Shared._Onyx.Blob;
using Content.Shared._Onyx.Blob.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Blob;

public sealed partial class BlobbernautSystem : SharedBlobbernautSystem
{
}

public sealed partial class BlobbernautVisualizerSystem : VisualizerSystem<BlobbernautComponent>
{
    private static readonly DamageStateVisualLayers[] Layers =
    [
        DamageStateVisualLayers.Base,
        DamageStateVisualLayers.BaseUnshaded,
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobbernautComponent, AfterAutoHandleStateEvent>(OnBlobbernautHandleState);
    }

    private void UpdateAppearance(EntityUid id, BlobbernautComponent blobbernaut, AppearanceComponent? appearance = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(id, ref appearance, ref sprite))
            return;

        foreach (var key in Layers)
        {
            if (!sprite.LayerMapTryGet(key, out _))
                continue;

            sprite.LayerSetColor(key, blobbernaut.Color);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, BlobbernautComponent component, ref AppearanceChangeEvent args)
    {
        UpdateAppearance(uid, component, args.Component, args.Sprite);
    }

    private void OnBlobbernautHandleState(EntityUid uid, BlobbernautComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateAppearance(uid, component);
    }
}

// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.SubFloor;
using Content.Shared._Onyx.VentCrawling;
using Robust.Client.Player;

namespace Content.Client._Onyx.VentCrawling;

public sealed partial class VentCrawlingVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SubFloorHideSystem _subFloorHideSystem = default!;

    private readonly HashSet<EntityUid> _visibleTubes = [];

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var player = _player.LocalEntity;
        _visibleTubes.Clear();

        if (TryComp(player, out VentCrawlerComponent? crawler) &&
            crawler.InTube)
        {
            foreach (var tube in crawler.VisibleTubes)
            {
                if (Exists(tube))
                    _visibleTubes.Add(tube);
            }
        }

        _subFloorHideSystem.SetVentPipes(_visibleTubes);
    }
}

// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Onyx.Fishing.Overlays;
using Content.Shared._Onyx.Fishing.Components;
using Content.Shared._Onyx.Fishing.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Fishing;

public sealed partial class FishingSystem : SharedFishingSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new FishingOverlay(EntityManager, _player));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<FishingOverlay>();
    }

    protected override void SetupFishingFloat(Entity<FishingRodComponent> fishingRod, EntityUid player, EntityCoordinates target) { }
    protected override void ThrowFishReward(EntProtoId fishId, EntityUid fishSpot, EntityUid target) { }
    protected override void CalculateFightingTimings(Entity<ActiveFisherComponent> fisher, ActiveFishingSpotComponent activeSpotComp) { }
}

// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Abductor.Glands.StatusEffects;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Abductor.Glands.StatusEffects;

public sealed partial class ScrambleLocationEffectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ScrambleLocationEffectComponent, ComponentInit>(OnInit);
    }
    private void OnInit(EntityUid uid, ScrambleLocationEffectComponent component, ComponentInit args)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        var origin = Transform(uid).Coordinates;
        for (var i = 0; i < 20; i++)
        {
            var target = origin.Offset(_random.NextAngle().ToVec() * _random.NextFloat(10f, 20f));
            if (!_turf.TryGetTileRef(target, out var tile)
                || _turf.IsSpace(tile.Value)
                || _turf.IsTileBlocked(tile.Value, (CollisionGroup) physics.CollisionMask))
                continue;

            _transform.SetCoordinates(uid, target);
            return;
        }
    }


}

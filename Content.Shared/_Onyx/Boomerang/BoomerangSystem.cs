// SPDX-FileCopyrightText: 2025 ActiveMammmoth <140334666+ActiveMammmoth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared._Onyx.Boomerang;

public sealed partial class BoomerangSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private readonly List<(EntityUid Uid, EntityCoordinates Coordinates, float Speed, EntityUid? Thrower)> _toThrow = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoomerangComponent, LandEvent>(OnLanded);
        SubscribeLocalEvent<BoomerangComponent, ThrownEvent>(OnThrown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (uid, coordinates, speed, thrower) in _toThrow)
        {
            if (!TerminatingOrDeleted(uid) && (thrower == null || !TerminatingOrDeleted(thrower)))
                _throwing.TryThrow(uid, coordinates, speed, user: thrower, recoil: false, playSound: false);
        }

        _toThrow.Clear();
    }

    private void OnThrown(Entity<BoomerangComponent> ent, ref ThrownEvent args)
    {
        if (ent.Comp.Thrower == null)
            SetThrower(ent, args.User);
    }

    private void OnLanded(Entity<BoomerangComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.Thrower is not { } thrower)
            return;

        if (TerminatingOrDeleted(thrower) || ent.Comp.CurrentHops >= ent.Comp.MaxHops)
        {
            SetThrower(ent, null);
            return;
        }

        var throwerCoordinates = Transform(thrower).Coordinates;
        if (!Transform(ent).Coordinates.TryDistance(EntityManager, throwerCoordinates, out var distance))
        {
            SetThrower(ent, null);
            return;
        }

        if (distance < ent.Comp.PickupDistance)
        {
            if (!_hands.TryPickup(thrower, ent))
                _toThrow.Add((ent, throwerCoordinates, ent.Comp.ReturnSpeed, null));

            SetThrower(ent, null);
            return;
        }

        _toThrow.Add((ent, throwerCoordinates, ent.Comp.ReturnSpeed, thrower));
        ent.Comp.CurrentHops++;
        Dirty(ent);
    }

    public void SetThrower(Entity<BoomerangComponent> ent, EntityUid? thrower)
    {
        ent.Comp.Thrower = thrower;
        ent.Comp.CurrentHops = 0;
        Dirty(ent);
    }
}

// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server._Onyx.AnimationData;
using Content.Shared._Onyx.Movement;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Movement;

public sealed partial class JumpSystem : SharedJumpSystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void OnJumpStarted(Entity<JumpComponent> ent) =>
        _animation.PlayAnimation(
            ent,
            "EmoteJump",
            Filter.Pvs(ent).RemoveWhereAttachedEntity(attached => attached == ent.Owner));

    protected override void OnJumpLanded(Entity<JumpComponent> ent)
    {
        if (!_mobState.IsAlive(ent) ||
            _standing.IsDown((ent.Owner, null)) ||
            !_random.Prob(_random.Next(5, 11) / 100f))
            return;

        _popup.PopupEntity(
            Loc.GetString("jump-stumble-self"),
            Loc.GetString("jump-stumble-others", ("jumper", Identity.Entity(ent, EntityManager))),
            ent,
            ent,
            PopupType.MediumCaution);
        _stun.TryKnockdown((ent.Owner, null), TimeSpan.FromSeconds(2), force: true);
    }
}

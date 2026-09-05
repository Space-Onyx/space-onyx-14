// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Robust.Shared.Physics;

namespace Content.Shared.Climbing.Systems;

public sealed partial class ClimbSystem
{
    public bool StartJumpClimb(Entity<ClimbingComponent> ent)
    {
        return TryComp(ent, out FixturesComponent? fixtures) && ReplaceFixtures(ent, ent.Comp, fixtures);
    }

    public void FinishJumpClimb(Entity<ClimbingComponent> ent)
    {
        if (!TryComp(ent, out FixturesComponent? fixtures) ||
            !fixtures.Fixtures.TryGetValue(ClimbingFixtureName, out var climbFixture))
            return;

        foreach (var contact in climbFixture.Contacts.Values)
        {
            if (!contact.IsTouching)
                continue;

            var climbable = ent.Owner == contact.EntityA ? contact.EntityB : contact.EntityA;
            if (!TryComp(climbable, out ClimbableComponent? climbableComp))
                continue;

            ent.Comp.IsClimbing = true;
            ent.Comp.NextTransition = null;
            Dirty(ent);

            var start = new StartClimbEvent(climbable);
            var climbed = new ClimbedOnEvent(ent, ent);
            RaiseLocalEvent(ent, ref start);
            RaiseLocalEvent(climbable, ref climbed);
            return;
        }

        StopClimb(ent, ent.Comp, fixtures);
    }
}

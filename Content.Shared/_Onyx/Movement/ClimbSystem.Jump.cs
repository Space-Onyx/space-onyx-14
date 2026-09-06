// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Shared.Climbing.Systems;

public sealed partial class ClimbSystem
{
    private const int JumpClimbGroup = (int) (CollisionGroup.TableLayer | CollisionGroup.LowImpassable);

    public bool StartJumpClimb(Entity<ClimbingComponent> ent, Entity<ClimbableComponent> climbable)
    {
        if (!TryComp(ent, out FixturesComponent? fixtures))
            return false;

        EnsureClimbFixtures(ent, fixtures);
        ent.Comp.IsClimbing = true;
        ent.Comp.NextTransition = null;
        Dirty(ent);

        var start = new StartClimbEvent(climbable);
        var climbed = new ClimbedOnEvent(ent, ent);
        RaiseLocalEvent(ent, ref start);
        RaiseLocalEvent(climbable, ref climbed);
        return true;
    }

    public void EnsureMountedState(Entity<ClimbingComponent> ent)
    {
        if (!TryComp(ent, out FixturesComponent? fixtures))
            return;

        EnsureClimbFixtures(ent, fixtures);
        ent.Comp.IsClimbing = true;
        ent.Comp.NextTransition = null;
        Dirty(ent);
    }

    private void EnsureClimbFixtures(Entity<ClimbingComponent> ent, FixturesComponent fixtures)
    {
        // Tolerate any prior partial state: upsert instead of Add, strip only present bits.
        foreach (var (name, fixture) in fixtures.Fixtures)
        {
            if (name == ClimbingFixtureName || !fixture.Hard)
                continue;
            if ((fixture.CollisionMask & JumpClimbGroup) == 0)
                continue;
            ent.Comp.DisabledFixtureMasks[name] = fixture.CollisionMask & JumpClimbGroup;
            _physics.SetCollisionMask(ent.Owner, name, fixture, fixture.CollisionMask & ~JumpClimbGroup, fixtures);
        }

        if (!fixtures.Fixtures.ContainsKey(ClimbingFixtureName))
        {
            _fixtureSystem.TryCreateFixture(
                ent.Owner,
                new PhysShapeCircle(0.35f),
                ClimbingFixtureName,
                collisionLayer: (int) CollisionGroup.None,
                collisionMask: JumpClimbGroup,
                hard: false,
                manager: fixtures);
        }
    }

    public void FinishJumpClimb(Entity<ClimbingComponent> ent)
    {
        if (!TryComp(ent, out FixturesComponent? fixtures))
            return;

        StopClimb(ent, ent.Comp, fixtures);
    }
}

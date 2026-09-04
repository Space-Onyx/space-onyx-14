// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem
{
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    private static readonly EntProtoId VampireSlowdown = "VampireSlowdownStatusEffect";

    private void OnSlowdownOnContactCollide(Entity<SlowdownOnContactComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (_entityWhitelist.IsWhitelistPass(ent.Comp.Blacklist, args.OtherEntity))
            return;

        _movementMod.TryUpdateMovementSpeedModDuration(args.OtherEntity, VampireSlowdown, ent.Comp.Duration, ent.Comp.Multiplier);
    }
}

// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Genetics;
using Robust.Shared.Random;

namespace Content.Server.Genetics.System;

public sealed partial class IncendiaryMitochondriaSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IncendiaryMitochondriaGenComponent>();
        while (query.MoveNext(out var uid, out var incendiaryMitochondria))
        {
            if (incendiaryMitochondria.NextTimeTick <= 0)
            {
                incendiaryMitochondria.NextTimeTick = 60;
                if (_random.Next(0, 100) < 50)
                {
                    if (TryComp(uid, out FlammableComponent? flammable))
                    {
                        flammable.FireStacks = 1f;
                        _flammable.Ignite(uid, uid);
                    }
                }
            }
            incendiaryMitochondria.NextTimeTick -= frameTime;
        }
    }
}


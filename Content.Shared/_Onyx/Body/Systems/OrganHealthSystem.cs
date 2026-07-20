using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Body.Systems;

public sealed partial class OrganHealthSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<OrganComponent>();
        while (query.MoveNext(out var uid, out var organ))
        {
            if (organ.Health > FixedPoint2.Zero)
                continue;

            QueueDel(uid);
        }
    }
}

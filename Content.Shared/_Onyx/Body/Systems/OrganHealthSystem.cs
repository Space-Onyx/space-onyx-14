using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Body.Systems;

public sealed partial class OrganHealthSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodySystem _body = default!;

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<OrganComponent>();
        while (query.MoveNext(out var uid, out var organ))
        {
            if (organ.Health > FixedPoint2.Zero)
                continue;

            if (HasComp<BrainComponent>(uid))
            {
                if (organ.Health != FixedPoint2.Zero)
                {
                    organ.Health = FixedPoint2.Zero;
                    Dirty(uid, organ);
                }

                continue;
            }

            DestroyOrgan(uid);
        }
    }

    private void DestroyOrgan(EntityUid uid)
    {
        var parent = Transform(uid).ParentUid;
        if (TryComp<BodyPartComponent>(parent, out var part))
        {
            foreach (var slot in part.Organs)
            {
                if (!_body.TryGetOrganInSlot(parent, slot, out var organ) || organ != uid)
                    continue;

                if (_body.TryRemoveOrgan(parent, slot, out var removed))
                {
                    QueueDel(removed);
                    return;
                }

                break;
            }
        }

        QueueDel(uid);
    }
}

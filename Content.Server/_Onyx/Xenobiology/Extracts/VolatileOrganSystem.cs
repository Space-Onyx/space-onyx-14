using Content.Server.Lightning;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared.Body;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenobiology.Extracts;

public sealed partial class VolatileOrganSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VolatileOrganComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<VolatileOrganComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    private void OnInserted(Entity<VolatileOrganComponent> organ, ref OrganGotInsertedEvent args)
    {
        organ.Comp.NextArc = _timing.CurTime + organ.Comp.MaxInterval;
        EnsureComp<VolatileOrganUserComponent>(args.Target).Organ = organ;
    }

    private void OnRemoved(Entity<VolatileOrganComponent> organ, ref OrganGotRemovedEvent args)
    {
        RemComp<VolatileOrganUserComponent>(args.Target);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VolatileOrganUserComponent>();
        while (query.MoveNext(out var user, out var active))
        {
            if (!TryComp<VolatileOrganComponent>(active.Organ, out var organ))
            {
                RemCompDeferred<VolatileOrganUserComponent>(user);
                continue;
            }

            if (_timing.CurTime < organ.NextArc)
                continue;

            var arcs = _random.Next(1, organ.MaxLightningArcs + 1);
            _lightning.ShootRandomLightnings(user, organ.Range, arcs, arcDepth: organ.ArcDepth);
            organ.NextArc = _timing.CurTime + _random.Next(organ.MinInterval, organ.MaxInterval);
        }
    }
}

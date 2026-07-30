using Content.Shared._Onyx.Salvage.BookOfGreentext;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Salvage.BookOfGreentext;

public sealed partial class CurseOfBookOfGreentextSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<ContainerManagerComponent> _containers;

    public override void Initialize()
    {
        _containers = GetEntityQuery<ContainerManagerComponent>();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CurseOfBookOfGreentextComponent>();
        while (query.MoveNext(out var uid, out var curse))
        {
            if (curse.NextUpdate >= _timing.CurTime)
                continue;

            curse.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(5);
            SetCompleted((uid, curse), ContainsLinkedBook(uid, curse.Book));
        }
    }

    private bool ContainsLinkedBook(EntityUid uid, EntityUid? linkedBook)
    {
        if (linkedBook == null || !_containers.TryGetComponent(uid, out var current))
            return false;

        var pending = new Stack<ContainerManagerComponent>();
        do
        {
            foreach (var container in current.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (contained == linkedBook && HasComp<BookOfGreentextComponent>(contained))
                        return true;

                    if (_containers.TryGetComponent(contained, out var nested))
                        pending.Push(nested);
                }
            }
        } while (pending.TryPop(out current));

        return false;
    }

    private void SetCompleted(Entity<CurseOfBookOfGreentextComponent> ent, bool completed)
    {
        if (ent.Comp.Completed == completed)
            return;

        ent.Comp.Completed = completed;
        Dirty(ent);
    }
}

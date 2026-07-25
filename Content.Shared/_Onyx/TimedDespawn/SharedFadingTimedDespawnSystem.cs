using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.TimedDespawn;

public abstract partial class SharedFadingTimedDespawnSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;

    private readonly HashSet<EntityUid> _queued = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FadingTimedDespawnComponent, AfterAutoHandleStateEvent>(OnAfterState);
        UpdatesOutsidePrediction = true;
    }

    private void OnAfterState(Entity<FadingTimedDespawnComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.FadeOutStarted)
            FadeOut(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!Timing.IsFirstTimePredicted)
            return;

        _queued.Clear();
        var query = EntityQueryEnumerator<FadingTimedDespawnComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!CanDelete(uid))
                continue;

            comp.Lifetime -= frameTime;
            if (comp.Lifetime > 0f)
                continue;

            if (comp.FadeOutTime <= 0f)
            {
                _queued.Add(uid);
                continue;
            }

            if (!comp.FadeOutStarted)
            {
                comp.FadeOutStarted = true;
                comp.Lifetime += comp.FadeOutTime;
                FadeOut((uid, comp));
                Dirty(uid, comp);
                continue;
            }

            _queued.Add(uid);
        }

        foreach (var uid in _queued)
        {
            var ev = new TimedDespawnEvent();
            RaiseLocalEvent(uid, ref ev);
            QueueDel(uid);
        }
    }

    protected virtual void FadeOut(Entity<FadingTimedDespawnComponent> ent)
    {
    }

    protected abstract bool CanDelete(EntityUid uid);
}

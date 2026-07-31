namespace Content.Shared._Onyx.Bloodtrak;

public abstract partial class SharedBloodtrakSystem : EntitySystem
{
    protected void SetDistance(Entity<BloodtrakComponent> ent, BloodtrakDistance distance)
    {
        if (ent.Comp.DistanceToTarget == distance)
            return;

        ent.Comp.DistanceToTarget = distance;
        Dirty(ent);
    }

    protected void SetActive(Entity<BloodtrakComponent> ent, bool active)
    {
        if (ent.Comp.IsActive == active)
            return;

        ent.Comp.IsActive = active;
        Dirty(ent);
    }
}

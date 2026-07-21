using Content.Shared.Trigger;

namespace Content.Server._Onyx.Trigger;

public sealed class DeleteParentOnTriggerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeleteParentOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<DeleteParentOnTriggerComponent> entity, ref TriggerEvent args)
    {
        QueueDel(Transform(entity).ParentUid);
        args.Handled = true;
    }
}

// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
using Content.Shared.Trigger;

namespace Content.Server._Onyx.Trigger;

public sealed class ExecuteTargetEventsOnTriggerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteTargetEventsOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<ExecuteTargetEventsOnTriggerComponent> entity, ref TriggerEvent ev)
    {
        foreach (var targetEvent in entity.Comp.Events)
        {
            targetEvent.Target = entity;
            RaiseLocalEvent(entity, (object) targetEvent, true);
        }
    }
}

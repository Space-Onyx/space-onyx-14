using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Clothing.Systems;

public sealed partial class ModifyDelayedKnockdownSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifyDelayedKnockdownComponent, DelayedKnockdownAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ModifyDelayedKnockdownComponent, InventoryRelayedEvent<DelayedKnockdownAttemptEvent>>(OnInventoryAttempt);
    }

    private void OnInventoryAttempt(Entity<ModifyDelayedKnockdownComponent> ent, ref InventoryRelayedEvent<DelayedKnockdownAttemptEvent> args) => Apply(ent.Comp, args.Args);
    private void OnAttempt(Entity<ModifyDelayedKnockdownComponent> ent, ref DelayedKnockdownAttemptEvent args) => Apply(ent.Comp, args);

    private static void Apply(ModifyDelayedKnockdownComponent component, DelayedKnockdownAttemptEvent args)
    {
        if (component.Cancel)
        {
            args.Cancel();
            return;
        }

        args.DelayDelta += component.DelayDelta;
        args.KnockdownTimeDelta += component.KnockdownTimeDelta;
    }
}

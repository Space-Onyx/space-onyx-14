using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Clothing;

public sealed class DelayedKnockdownAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.OUTERCLOTHING;
    public float DelayDelta;
    public float KnockdownTimeDelta;
}

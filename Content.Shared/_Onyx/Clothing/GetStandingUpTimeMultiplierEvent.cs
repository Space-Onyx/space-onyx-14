using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Clothing;

public sealed class GetStandingUpTimeMultiplierEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.FEET;
    public float Multiplier = 1f;
}

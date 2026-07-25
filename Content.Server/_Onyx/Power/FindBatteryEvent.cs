using Content.Shared.Power.Components;
using Content.Shared.Inventory;

namespace Content.Server.Power.EntitySystems;

[ByRefEvent]
public record struct FindBatteryEvent : IInventoryRelayEvent
{
    public FindBatteryEvent()
    {
        FoundBattery = null;
    }

    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public Entity<BatteryComponent>? FoundBattery;
}

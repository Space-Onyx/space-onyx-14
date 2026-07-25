using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Mobs;

namespace Content.Shared._Onyx.Clothing;

public record struct ClothingAutoInjectRelayedEvent(EntityUid Target, MobState NewState) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

public sealed partial class ActionActivateAutoInjectorEvent : InstantActionEvent;

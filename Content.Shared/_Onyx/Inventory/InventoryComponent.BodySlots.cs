namespace Content.Shared.Inventory;

public sealed partial class InventoryComponent
{
    /// <summary>
    /// Slots currently supported by the entity's body parts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags AvailableSlots = SlotFlags.All;

    /// <summary>
    /// Whether missing body parts disable their associated inventory slots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BodySlotFiltering = true;
}

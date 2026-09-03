using Content.Shared.Whitelist;

#pragma warning disable IDE0130
namespace Content.Shared.Containers.ItemSlots;
#pragma warning restore IDE0130

public sealed partial class ItemSlotsSystem
{
    public ItemSlot AddAugmentModuleSlot(
        Entity<ItemSlotsComponent?> ent,
        string id,
        string name,
        EntityWhitelist? whitelist)
    {
        var slot = new ItemSlot
        {
            Name = name,
            Whitelist = whitelist,
            ShowVerbs = false,
            InsertOnInteract = false,
            Swap = false,
            EjectOnBreak = true,
        };
        AddItemSlot(ent, id, slot);
        return slot;
    }
}

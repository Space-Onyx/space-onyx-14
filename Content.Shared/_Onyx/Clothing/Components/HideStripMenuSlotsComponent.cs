using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HideStripMenuSlotsComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;
}

[ByRefEvent]
public record struct IsStripMenuSlotHiddenEvent(SlotFlags Slot, bool Hidden = false);

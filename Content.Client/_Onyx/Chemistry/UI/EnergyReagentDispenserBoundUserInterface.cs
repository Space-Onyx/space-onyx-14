using Content.Client.UserInterface.Controls;
using Content.Shared._Onyx.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Chemistry.UI;

public sealed class EnergyReagentDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private EnergyReagentDispenserWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<EnergyReagentDispenserWindow>();
        _window.SetInfoFromEntity(EntMan, Owner);
        _window.EjectButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(SharedEnergyReagentDispenser.OutputSlotName));
        _window.ClearButton.OnPressed += _ => SendMessage(new EnergyReagentDispenserClearContainerSolutionMessage());
        _window.AmountGrid.OnButtonPressed += amount => SendMessage(new EnergyReagentDispenserSetDispenseAmountMessage(amount));
        _window.Dispense += id => SendMessage(new EnergyReagentDispenserDispenseReagentMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is EnergyReagentDispenserBoundUserInterfaceState energy)
            _window?.UpdateState(energy);
    }
}

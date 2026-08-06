using Content.Shared.Atmos.Components;
using Content.Shared._Onyx.ZLevels.Monitoring; // <Onyx-ZLevels>

namespace Content.Client.Atmos.Consoles;

public sealed class AtmosMonitoringConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AtmosMonitoringConsoleWindow? _menu;

    public AtmosMonitoringConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = new AtmosMonitoringConsoleWindow(this, Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;
        _menu.SendZLevelSelectedMessageAction += SendZLevelSelectedMessage; // <Onyx-ZLevels>
    }

    // <Onyx-ZLevels>
    private void SendZLevelSelectedMessage(NetEntity? grid, int depth)
    {
        SendMessage(new CEZMonitoringConsoleLevelSelectedMessage(grid, depth));
    }
    // </Onyx-ZLevels>

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AtmosMonitoringConsoleBoundInterfaceState castState)
            return;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.UpdateUI(xform?.Coordinates, castState.AtmosNetworks);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}

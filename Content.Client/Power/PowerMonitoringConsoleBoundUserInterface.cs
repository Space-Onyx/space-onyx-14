using Content.Shared.Power;
using Content.Shared._Onyx.ZLevels.Monitoring; // <Onyx-ZLevels>
using Robust.Client.UserInterface;

namespace Content.Client.Power;

public sealed class PowerMonitoringConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PowerMonitoringWindow? _menu;

    public PowerMonitoringConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<PowerMonitoringWindow>();
        _menu.SetEntity(Owner);
        _menu.SendPowerMonitoringConsoleMessageAction += SendPowerMonitoringConsoleMessage;
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

        var castState = (PowerMonitoringConsoleBoundInterfaceState) state;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.ShowEntites
            (castState.TotalSources,
            castState.TotalBatteryUsage,
            castState.TotalLoads,
            castState.AllEntries,
            castState.FocusSources,
            castState.FocusLoads,
            xform?.Coordinates);
    }

    public void SendPowerMonitoringConsoleMessage(NetEntity? netEntity, PowerMonitoringConsoleGroup group)
    {
        SendMessage(new PowerMonitoringConsoleMessage(netEntity, group));
    }
}

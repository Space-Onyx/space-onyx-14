using Content.Shared.Medical.CrewMonitoring;
using Content.Shared._Onyx.ZLevels.Monitoring; // <Onyx-ZLevels>
using Robust.Client.UserInterface;

namespace Content.Client.Medical.CrewMonitoring;

public sealed class CrewMonitoringBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CrewMonitoringWindow? _menu;

    public CrewMonitoringBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
            {
                stationName = metaData.EntityName;
            }
        }

        _menu = this.CreateWindow<CrewMonitoringWindow>();
        _menu.Set(stationName, gridUid);
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

        switch (state)
        {
            case CrewMonitoringState st:
                EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
                _menu?.ShowSensors(st.Sensors, Owner, xform?.Coordinates);
                break;
        }
    }
}

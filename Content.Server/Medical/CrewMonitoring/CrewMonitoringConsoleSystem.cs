using System.Linq;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared._Onyx.CrewMonitoring; // <Onyx-CommandTrackingImplant>
using Content.Shared._Onyx.ZLevels.Core.Components; // <Onyx-ZLevels>
using Content.Shared._Onyx.ZLevels.Monitoring; // <Onyx-ZLevels>
using Content.Shared.PowerCell;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components; // <Onyx-ZLevels>

namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, CEZMonitoringConsoleLevelSelectedMessage>(OnZLevelSelected); // <Onyx-ZLevels>
    }

    // <Onyx-ZLevels>
    private void OnZLevelSelected(EntityUid uid, CrewMonitoringConsoleComponent component, CEZMonitoringConsoleLevelSelectedMessage args)
    {
        var target = GetEntity(args.Grid);
        var source = Transform(uid).GridUid;
        if (target is not { } grid || source is not { } sourceGrid || !HasComp<MapGridComponent>(grid))
            return;

        if (sourceGrid != grid && (!TryComp<CEZLinkedGridComponent>(sourceGrid, out var sourceLinked) ||
            !TryComp<CEZLinkedGridComponent>(grid, out var targetLinked) || !sourceLinked.LinkNetwork.IsValid() ||
            sourceLinked.LinkNetwork != targetLinked.LinkNetwork))
            return;

        EnsureComp<NavMapComponent>(grid);
    }
    // </Onyx-ZLevels>

    private void OnRemove(EntityUid uid, CrewMonitoringConsoleComponent component, ComponentRemove args)
    {
        component.ConnectedSensors.Clear();
    }

    private void OnPacketReceived(EntityUid uid, CrewMonitoringConsoleComponent component, DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;

        // Check command
        if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        if (command != DeviceNetworkConstants.CmdUpdatedState)
            return;

        if (!payload.TryGetValue(SuitSensorConstants.NET_STATUS_COLLECTION, out Dictionary<string, SuitSensorStatus>? sensorStatus))
            return;

        component.ConnectedSensors = sensorStatus;
        UpdateUserInterface(uid, component);
    }

    private void OnUIOpened(EntityUid uid, CrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // <Onyx-CommandTrackingImplant-edited>
        var commandOnly = HasComp<CrewMonitorScanningComponent>(uid);
        var allSensors = component.ConnectedSensors.Values
            .Where(sensor => sensor.IsCommandTracker == commandOnly)
            .ToList();
        // </Onyx-CommandTrackingImplant-edited>
        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(allSensors));
    }
}

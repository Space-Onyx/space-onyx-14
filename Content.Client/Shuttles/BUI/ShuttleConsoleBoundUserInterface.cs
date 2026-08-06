using Content.Client.Shuttles.UI;
using Content.Shared._Onyx.ZLevels.Shuttles; // <Onyx-ZLevels>
// <ShuttleSignalPorts>
using Content.Shared._Onyx.Shuttles.Events;
// </ShuttleSignalPorts>
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed partial class ShuttleConsoleBoundUserInterface : BoundUserInterface // <Onyx-ShuttleDampening-edited>
{
    [ViewVariables]
    private ShuttleConsoleWindow? _window;

    public ShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ShuttleConsoleWindow>();

        _window.RequestFTL += OnFTLRequest;
        _window.RequestBeaconFTL += OnFTLBeaconRequest;
        _window.DockRequest += OnDockRequest;
        _window.UndockRequest += OnUndockRequest;
        _window.RequestFlyUp += () => SendMessage(new CEShuttleConsoleFlyUpMessage()); // <Onyx-ZLevels>
        _window.RequestFlyDown += () => SendMessage(new CEShuttleConsoleFlyDownMessage()); // <Onyx-ZLevels>
        _window.NetworkPortButtonPressed += OnNetworkPortButtonPressed; // <ShuttleSignalPorts>
        InitializeDampening(); // <Onyx-ShuttleDampening>
    }

    // <ShuttleSignalPorts>
    private void OnNetworkPortButtonPressed(string sourcePort)
    {
        SendMessage(new ShuttlePortButtonPressedMessage { SourcePort = sourcePort });
    }
    // </ShuttleSignalPorts>

    private void OnUndockRequest(NetEntity entity)
    {
        SendMessage(new UndockRequestMessage()
        {
            DockEntity = entity,
        });
    }

    private void OnDockRequest(NetEntity entity, NetEntity target)
    {
        SendMessage(new DockRequestMessage()
        {
            DockEntity = entity,
            TargetDockEntity = target,
        });
    }

    private void OnFTLBeaconRequest(NetEntity ent, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLBeaconMessage()
        {
            Beacon = ent,
            Angle = angle,
        });
    }

    private void OnFTLRequest(MapCoordinates obj, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLPositionMessage()
        {
            Coordinates = obj,
            Angle = angle,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ShuttleBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(Owner, cState);
    }
}

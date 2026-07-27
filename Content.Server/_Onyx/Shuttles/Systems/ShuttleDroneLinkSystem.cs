using Content.Server.Shuttles;
using Content.Server.Shuttles.Systems;
using Content.Shared.DeviceLinking.Events;

namespace Content.Server._Onyx.Shuttles.Systems;

public sealed partial class ShuttleDroneLinkSystem : EntitySystem
{
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;

    public const string RemoteDroneTag = "DroneShuttleLinkable";
    public const string RemoteDroneSourcePort = "ShuttleDroneTransmitter";
    public const string RemoteDroneSinkPort = "ShuttleDroneReceiver";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DroneConsoleComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<DroneConsoleComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<DroneConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }

    private void OnLinkAttempt(Entity<DroneConsoleComponent> entity, ref LinkAttemptEvent args)
    {
        if (args.SourcePort == RemoteDroneSourcePort &&
            args.SinkPort == RemoteDroneSinkPort &&
            !HasComp<DroneConsoleComponent>(args.Sink))
            return;

        args.Cancel();
    }

    private void OnNewLink(Entity<DroneConsoleComponent> entity, ref NewLinkEvent args)
    {
        RefreshConsoles();
    }

    private void OnPortDisconnected(Entity<DroneConsoleComponent> entity, ref PortDisconnectedEvent args)
    {
        if (args.Port == RemoteDroneSourcePort)
            RefreshConsoles();
    }

    private void RefreshConsoles()
    {
        _shuttleConsole.RefreshDroneConsoles();
        _shuttleConsole.RefreshShuttleConsoles();
    }
}

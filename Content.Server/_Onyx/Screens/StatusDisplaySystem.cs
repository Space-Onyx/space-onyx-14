using Content.Server.AlertLevel;
using Content.Server.Screens.Components;
using Content.Shared._Onyx.Communications;
using Content.Shared._Onyx.Screens;
using Content.Shared.AlertLevel;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.RoundEnd;
using Content.Shared.Station.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Screens;

public sealed partial class StatusDisplaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StatusDisplayComponent, DeviceNetworkPacketEvent<StatusDisplayConfigurationPayload>>(OnConfigurationPacket);
        SubscribeLocalEvent<StatusDisplayComponent, DeviceNetworkPacketEvent<ScreenShuttlePayload>>(OnShuttlePacket);
        SubscribeLocalEvent<StatusDisplayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnMapInit(Entity<StatusDisplayComponent> ent, ref MapInitEvent args)
    {
        if (_station.GetOwningStation(ent) is { } station && TryComp<AlertLevelComponent>(station, out var alert))
            ent.Comp.AlertLevel = alert.CurrentAlertLevel;

        UpdateVisuals(ent);
    }

    private void OnConfigurationPacket(Entity<StatusDisplayComponent> ent, ref DeviceNetworkPacketEvent<StatusDisplayConfigurationPayload> args)
    {
        if (!TryComp<DeviceNetworkComponent>(ent, out var network)
            || network.ReceiveFrequency is { } frequency && frequency != args.Frequency
            || Transform(ent).GridUid != args.Data.Grid)
            return;

        ent.Comp.Line1 = args.Data.Line1;
        ent.Comp.Line2 = args.Data.Line2;
        ent.Comp.ShowAlertBorder = args.Data.ShowBorders;
        ent.Comp.Content = args.Data.Content;

        Dirty(ent);
        UpdateVisuals(ent);
    }

    private void OnShuttlePacket(Entity<StatusDisplayComponent> ent, ref DeviceNetworkPacketEvent<ScreenShuttlePayload> args)
    {
        var transform = Transform(ent);
        var atDestination = args.Data.Docked;
        TimeSpan duration;
        switch (transform.MapUid)
        {
            case var local when local == args.Data.Shuttle || transform.GridUid == args.Data.Shuttle:
                duration = args.Data.ShuttleTime;
                break;
            case var origin when origin == args.Data.SourceMap:
                duration = args.Data.SourceTime;
                break;
            case var remote when remote == args.Data.DestinationMap:
                duration = args.Data.DestinationTime;
                atDestination = false;
                break;
            default:
                return;
        }

        ent.Comp.IsAtDestination = atDestination;
        ent.Comp.TargetTime = _timing.CurTime + duration;
    }

    private void OnAlertLevelChanged(ref AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<StatusDisplayComponent>();
        while (query.MoveNext(out var uid, out var display))
        {
            if (_station.GetOwningStation(uid) != args.Station)
                continue;

            display.AlertLevel = args.AlertLevel;
            Dirty(uid, display);
            UpdateVisuals((uid, display));
        }
    }

    private void UpdateVisuals(Entity<StatusDisplayComponent> ent)
    {
        _appearance.SetData(ent, StatusDisplayVisuals.Content, ent.Comp.Content);
        _appearance.SetData(ent, StatusDisplayVisuals.ShowAlertBorder, ent.Comp.ShowAlertBorder);
        _appearance.SetData(ent, StatusDisplayVisuals.AlertLevel, ent.Comp.AlertLevel.ToLowerInvariant());
    }
}

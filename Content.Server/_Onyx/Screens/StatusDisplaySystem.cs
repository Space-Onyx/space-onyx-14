using Content.Server.AlertLevel;
using Content.Server.Screens.Components;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Communications;
using Content.Shared._Onyx.Screens;
using Content.Shared.AlertLevel;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Screens;

public sealed partial class StatusDisplaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StatusDisplayComponent, DeviceNetworkPacketEvent>(OnPacket);
        SubscribeLocalEvent<StatusDisplayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnMapInit(Entity<StatusDisplayComponent> ent, ref MapInitEvent args)
    {
        if (_station.GetOwningStation(ent) is { } station && TryComp<AlertLevelComponent>(station, out var alert))
            ent.Comp.AlertLevel = alert.CurrentAlertLevel;

        UpdateVisuals(ent);
    }

    private void OnPacket(Entity<StatusDisplayComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(ent, out var network)
            || network.ReceiveFrequency is { } frequency && frequency != args.Frequency)
            return;

        if (args.Data.TryGetValue(ShuttleTimerMasks.ShuttleMap, out _))
            UpdateShuttleTimer(ent, args);

        if (args.Data.TryGetValue(ScreenPackets.Text, out (string, string)? text))
        {
            ent.Comp.Line1 = text.Value.Item1;
            ent.Comp.Line2 = text.Value.Item2;
        }

        if (args.Data.TryGetValue(ScreenPackets.ShowBorders, out bool? borders))
            ent.Comp.ShowAlertBorder = borders.Value;

        if (args.Data.TryGetValue(ScreenPackets.Content, out StatusDisplayContent? content))
            ent.Comp.Content = content.Value;

        Dirty(ent);
        UpdateVisuals(ent);
    }

    private void UpdateShuttleTimer(Entity<StatusDisplayComponent> ent, DeviceNetworkPacketEvent args)
    {
        var transform = Transform(ent);
        args.Data.TryGetValue(ShuttleTimerMasks.ShuttleMap, out EntityUid? shuttle);
        args.Data.TryGetValue(ShuttleTimerMasks.SourceMap, out EntityUid? source);
        args.Data.TryGetValue(ShuttleTimerMasks.DestMap, out EntityUid? destination);
        args.Data.TryGetValue(ShuttleTimerMasks.Docked, out bool docked);

        var atDestination = docked;
        string key;
        switch (transform.MapUid)
        {
            case var local when local == shuttle || transform.GridUid == shuttle:
                key = ShuttleTimerMasks.ShuttleTime;
                break;
            case var origin when origin == source:
                key = ShuttleTimerMasks.SourceTime;
                break;
            case var remote when remote == destination:
                key = ShuttleTimerMasks.DestTime;
                atDestination = false;
                break;
            default:
                return;
        }

        if (!args.Data.TryGetValue(key, out TimeSpan duration))
            return;

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

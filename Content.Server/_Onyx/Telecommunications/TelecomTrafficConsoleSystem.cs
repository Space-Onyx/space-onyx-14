using Content.Server._Onyx.Telecommunications.Components;
using Content.Shared._Onyx.Telecommunications;
using Content.Shared.DeviceLinking;
using Content.Shared.Labels.Components;
using Content.Shared.Lock;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Onyx.Telecommunications;

public sealed partial class TelecomTrafficConsoleSystem : EntitySystem
{
    private const float TelemetryUpdateInterval = 0.25f;
    private const float FullRefreshInterval = 5f;
    private const int TrafficBins = 12;
    private static readonly TimeSpan BinDuration = TimeSpan.FromSeconds(5);

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TelecommunicationsChainSystem _chain = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelecomTrafficConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<TelecomTrafficConsoleComponent, TelecomTrafficSelectServerMessage>(OnSelectServer);
        SubscribeLocalEvent<TelecomTrafficConsoleComponent, TelecomTrafficToggleRoutingMessage>(OnToggleRouting);
        SubscribeLocalEvent<TelecomTrafficConsoleComponent, TelecomTrafficToggleChannelMessage>(OnToggleChannel);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TelecomTrafficConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_ui.IsUiOpen(uid, TelecomTrafficConsoleUiKey.Key))
            {
                component.TelemetryAccumulator = 0f;
                component.FullRefreshAccumulator = 0f;
                continue;
            }

            component.TelemetryAccumulator += frameTime;
            component.FullRefreshAccumulator += frameTime;
            if (component.TelemetryAccumulator < TelemetryUpdateInterval)
                continue;

            component.TelemetryAccumulator %= TelemetryUpdateInterval;

            if (HasNewLog(component) ||
                component.FullRefreshAccumulator >= FullRefreshInterval)
            {
                component.FullRefreshAccumulator = 0f;
                UpdateUi((uid, component));
                continue;
            }

            SendTelemetry((uid, component));
        }
    }

    private void OnUiOpened(Entity<TelecomTrafficConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectServer(
        Entity<TelecomTrafficConsoleComponent> ent,
        ref TelecomTrafficSelectServerMessage args)
    {
        if (_lock.IsLocked(ent.Owner))
            return;

        var server = GetEntity(args.Server);
        if (!IsServerVisible(ent.Owner, server))
            return;

        ent.Comp.SelectedServer = server;
        UpdateUi(ent);
    }

    private void OnToggleRouting(
        Entity<TelecomTrafficConsoleComponent> ent,
        ref TelecomTrafficToggleRoutingMessage args)
    {
        if (_lock.IsLocked(ent.Owner) ||
            ent.Comp.SelectedServer is not { } server ||
            !IsServerVisible(ent.Owner, server) ||
            !TryComp<TelecomSignalLogComponent>(server, out var log))
        {
            return;
        }

        if (!args.Enabled)
        {
            log.RoutingEnabled = false;
            if (TryComp<TelecomRouterComponent>(server, out var router))
                router.ResetArmed = true;
        }
        else
        {
            if (TryComp<TelecomRouterComponent>(server, out var router) &&
                router.ResetArmed)
            {
                _chain.RepairRouter(server);
            }

            log.RoutingEnabled = true;
        }

        UpdateUi(ent);
    }

    private void OnToggleChannel(
        Entity<TelecomTrafficConsoleComponent> ent,
        ref TelecomTrafficToggleChannelMessage args)
    {
        if (_lock.IsLocked(ent.Owner) ||
            ent.Comp.SelectedServer is not { } server ||
            !IsServerVisible(ent.Owner, server) ||
            !TryComp<TelecomRouterComponent>(server, out var router) ||
            !_chain.ServerHasChannel(server, args.Channel))
        {
            return;
        }

        if (!router.DisabledChannels.Add(args.Channel))
            router.DisabledChannels.Remove(args.Channel);

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<TelecomTrafficConsoleComponent> ent)
    {
        ent.Comp.FullRefreshAccumulator = 0f;

        var servers = GetServers(ent.Owner);

        if (ent.Comp.SelectedServer == null ||
            servers.All(server => GetEntity(server.Entity) != ent.Comp.SelectedServer))
        {
            ent.Comp.SelectedServer = servers.Count > 0
                ? GetEntity(servers[0].Entity)
                : null;
        }

        var logs = new List<TelecomTrafficLogInfo>();
        var selectedChannels = new List<TelecomTrafficChannelInfo>();
        var routingEnabled = false;
        var estimatedCalibration = 0;
        var estimatedLoad = 0;
        var hardware = new List<TelecomHardwareInfo>();

        if (ent.Comp.SelectedServer is { } selected &&
            TryComp<TelecomSignalLogComponent>(selected, out var log))
        {
            routingEnabled = log.RoutingEnabled;
            hardware = _chain.GetServerHardwareInfo(selected);
            var metrics = _chain.GetServerMetrics(selected);
            estimatedCalibration = (int) MathF.Round(metrics.Quality * 100f);
            estimatedLoad = (int) MathF.Round(metrics.MaxUtilization * 100f);

            foreach (var channel in _chain.GetServerChannels(selected)
                         .Select(id => (
                             Id: id,
                             Name: _prototypes.TryIndex<RadioChannelPrototype>(id, out var prototype)
                                 ? prototype.LocalizedName
                                 : id))
                         .OrderBy(channel => channel.Name))
            {
                selectedChannels.Add(new TelecomTrafficChannelInfo(
                    channel.Id,
                    channel.Name,
                    _chain.IsServerChannelEnabled(selected, channel.Id)));
            }

            foreach (var entry in log.Entries.TakeLast(100).Reverse())
            {
                logs.Add(new TelecomTrafficLogInfo(
                    FormatTime(entry.Timestamp),
                    entry.Channel,
                    entry.Message,
                    SanitizeStatus(entry.Status),
                    entry.MessageLength,
                    entry.Timestamp.TotalSeconds,
                    entry.SignalQuality,
                    entry.LoadPercent,
                    entry.LatencyMilliseconds));
            }
        }

        _ui.SetUiState(
            ent.Owner,
            TelecomTrafficConsoleUiKey.Key,
            new TelecomTrafficConsoleState(
                servers,
                ent.Comp.SelectedServer is { } uid ? GetNetEntity(uid) : null,
                routingEnabled,
                selectedChannels,
                logs,
                TrafficBins,
                (int) BinDuration.TotalSeconds,
                _timing.CurTime.TotalSeconds,
                estimatedCalibration,
                estimatedLoad,
                hardware));

        ent.Comp.LastLogRevision = ent.Comp.SelectedServer is { } server &&
                                   TryComp<TelecomSignalLogComponent>(server, out var selectedLog)
            ? selectedLog.Revision
            : -1;
        ent.Comp.LastTelemetryHash = GetTelemetryHash(
            estimatedCalibration,
            estimatedLoad,
            hardware);
    }

    private bool HasNewLog(TelecomTrafficConsoleComponent component)
    {
        return component.SelectedServer is { } server &&
               TryComp<TelecomSignalLogComponent>(server, out var log) &&
               log.Revision != component.LastLogRevision;
    }

    private void SendTelemetry(Entity<TelecomTrafficConsoleComponent> ent)
    {
        if (ent.Comp.SelectedServer is not { } server ||
            !Exists(server))
            return;

        var metrics = _chain.GetServerMetrics(server);
        var quality = (int) MathF.Round(metrics.Quality * 100f);
        var load = (int) MathF.Round(metrics.MaxUtilization * 100f);
        var hardware = _chain.GetServerHardwareInfo(server);
        var hash = GetTelemetryHash(quality, load, hardware);

        if (hash == ent.Comp.LastTelemetryHash)
            return;

        ent.Comp.LastTelemetryHash = hash;
        _ui.ServerSendUiMessage(
            ent.Owner,
            TelecomTrafficConsoleUiKey.Key,
            new TelecomTrafficTelemetryMessage(quality, load, hardware));
    }

    private static int GetTelemetryHash(
        int quality,
        int load,
        List<TelecomHardwareInfo> hardware)
    {
        var hash = new HashCode();
        hash.Add(quality);
        hash.Add(load);

        foreach (var entry in hardware)
        {
            hash.Add(entry.Type);
            hash.Add(entry.Index);
            hash.Add(entry.Powered);
            hash.Add(entry.Calibration);
            hash.Add(entry.Wear);
            hash.Add(entry.LoadPercent);
        }

        return hash.ToHashCode();
    }

    private List<TelecomTrafficServerInfo> GetServers(EntityUid console)
    {
        var result = new List<TelecomTrafficServerInfo>();
        if (!TryComp<DeviceLinkSinkComponent>(console, out var sink))
            return result;

        foreach (var serverUid in sink.LinkedSources)
        {
            if (!_chain.IsServerLinkedToConsole(serverUid, console) ||
                !TryComp<TelecomSignalLogComponent>(serverUid, out var log))
                continue;

            var name = GetServerDisplayName(serverUid);

            result.Add(new TelecomTrafficServerInfo(
                GetNetEntity(serverUid),
                name,
                log.RoutingEnabled));
        }

        return result;
    }

    private string GetServerDisplayName(EntityUid serverUid)
    {
        if (TryComp<LabelComponent>(serverUid, out var label) &&
            !string.IsNullOrWhiteSpace(label.CurrentLabel))
            return label.CurrentLabel;

        return Loc.GetString("telecom-server-label-unassigned");
    }

    private bool IsServerVisible(EntityUid console, EntityUid server)
    {
        return Transform(console).MapID == Transform(server).MapID &&
               HasComp<TelecomServerComponent>(server) &&
               HasComp<TelecomSignalLogComponent>(server) &&
               _chain.IsServerLinkedToConsole(server, console);
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private static TelecomSignalStatus SanitizeStatus(TelecomSignalStatus status)
    {
        return status switch
        {
            TelecomSignalStatus.NoReceiver or
            TelecomSignalStatus.NoBus or
            TelecomSignalStatus.NoProcessor or
            TelecomSignalStatus.NoBroadcaster or
            TelecomSignalStatus.NoRoute => TelecomSignalStatus.SignalLoss,
            _ => status,
        };
    }
}

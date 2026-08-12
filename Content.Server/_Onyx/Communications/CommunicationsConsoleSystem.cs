using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Communications;
using Content.Shared._Onyx.Screens;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Communications;

public sealed partial class CommunicationsConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OnyxCommunicationsConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OnyxCommunicationsConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceive);
        Subs.BuiEvents<OnyxCommunicationsConsoleComponent>(OnyxCommunicationsConsoleUi.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<CommunicationsConsoleAnnouncementMessage>(OnAnnouncement);
            subs.Event<CommunicationsConsoleAlertLevelMessage>(OnAlertLevel);
            subs.Event<CommunicationsConsoleEvacuationShuttleMessage>(OnEvacuationShuttle);
            subs.Event<CommunicationsConsoleScreenConfigurationMessage>(OnScreenConfiguration);
        });
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AlertLevelDelayFinishedEvent>(OnAlertLevelDelayFinished);
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEndChanged);
    }

    private void OnMapInit(Entity<OnyxCommunicationsConsoleComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CanAnnounceAt = _timing.CurTime + ent.Comp.InitialAnnouncementDelay;
        ent.Comp.ShuttlesCallable = CanCallOrRecall();
        RefreshStationState(ent);
        ent.Comp.ExpectedEvacuationArrival = _roundEnd.ExpectedCountdownEnd;
        ent.Comp.ExpectedEvacuationDuration = _roundEnd.ExpectedShuttleLength;
        Dirty(ent);
    }

    private void OnPacketReceive(Entity<OnyxCommunicationsConsoleComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (args.Data.TryGetValue(ScreenPackets.Grid, out EntityUid? grid) && Transform(ent).GridUid != grid)
            return;

        var changed = false;
        if (args.Data.TryGetValue(ScreenPackets.Text, out (string, string)? text))
        {
            ent.Comp.LastConfiguredLine1 = text.Value.Item1;
            ent.Comp.LastConfiguredLine2 = text.Value.Item2;
            changed = true;
        }
        if (args.Data.TryGetValue(ScreenPackets.ShowBorders, out bool? borders))
        {
            ent.Comp.LastConfiguredShowBorders = borders.Value;
            changed = true;
        }
        if (args.Data.TryGetValue(ScreenPackets.Content, out StatusDisplayContent? content))
        {
            ent.Comp.LastConfiguredContent = content.Value;
            changed = true;
        }
        if (changed)
            Dirty(ent);
    }

    private void OnAnnouncement(Entity<OnyxCommunicationsConsoleComponent> ent, ref CommunicationsConsoleAnnouncementMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !ent.Comp.CanAnnounce || _timing.CurTime < ent.Comp.CanAnnounceAt)
            return;

        if (!HasAccess(ent, actor))
        {
            _popup.PopupEntity(Loc.GetString("comms-console-permission-denied"), ent, actor, PopupType.Medium);
            return;
        }

        var message = SharedChatSystem.SanitizeAnnouncement(args.Announcement, _config.GetCVar(CCVars.ChatMaxAnnouncementLength));
        var author = _identity.GetIdentityShortInfo(actor, ent) ?? Loc.GetString("comms-console-announcement-unknown-sender");
        Loc.TryGetString(ent.Comp.AnnouncementTitle, out var title);
        title ??= ent.Comp.AnnouncementTitle;
        if (ent.Comp.AnnounceSentBy)
            message += "\n" + Loc.GetString("comms-console-announcement-sent-by") + " " + author;

        if (ent.Comp.GlobalAnnouncements)
            _chat.DispatchGlobalAnnouncement(message, title, announcementSound: ent.Comp.AnnouncementSound, colorOverride: ent.Comp.AnnouncementColor);
        else
            _chat.DispatchStationAnnouncement(ent, message, title, announcementSound: ent.Comp.AnnouncementSound, colorOverride: ent.Comp.AnnouncementColor);

        _adminLog.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(actor):player} sent announcement using {ToPrettyString(ent):console}: {message:message}");
        ent.Comp.CanAnnounceAt = _timing.CurTime + ent.Comp.AnnouncementInterval;
        Dirty(ent);
    }

    private void OnAlertLevel(Entity<OnyxCommunicationsConsoleComponent> ent, ref CommunicationsConsoleAlertLevelMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !ent.Comp.CanAlertLevel || RefreshStationState(ent) is not { } station)
            return;

        if (!HasAccess(ent, actor))
        {
            _popup.PopupEntity(Loc.GetString("comms-console-permission-denied"), ent, actor, PopupType.Medium);
            return;
        }

        if (!TryComp<AlertLevelComponent>(station, out var alert) || !_alertLevel.CanChangeAlertLevel((station, alert)))
            return;

        _alertLevel.SetLevel(station, new ProtoId<AlertLevelPrototype>(args.AlertLevel));
    }

    private void OnEvacuationShuttle(Entity<OnyxCommunicationsConsoleComponent> ent, ref CommunicationsConsoleEvacuationShuttleMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !ent.Comp.CanCallShuttles || !CanCallOrRecall())
            return;

        if (!HasAccess(ent, actor))
        {
            _popup.PopupEntity(Loc.GetString("comms-console-permission-denied"), ent, actor, PopupType.Medium);
            return;
        }

        if (!args.Call)
        {
            _roundEnd.CancelRoundEndCountdown(actor, ent);
            _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(actor):player} recalled shuttle using {ToPrettyString(ent):console}");
            return;
        }

        var ev = new Content.Server.Communications.CommunicationConsoleCallShuttleAttemptEvent(ent, default!, actor);
        RaiseLocalEvent(ref ev);
        if (ev.Cancelled)
        {
            _popup.PopupEntity(ev.Reason ?? Loc.GetString("comms-console-shuttle-unavailable"), ent, actor, PopupType.Medium);
            return;
        }

        _roundEnd.RequestRoundEnd(actor, ent);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(actor):player} called shuttle using {ToPrettyString(ent):console}");
    }

    private void OnScreenConfiguration(Entity<OnyxCommunicationsConsoleComponent> ent, ref CommunicationsConsoleScreenConfigurationMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !ent.Comp.CanConfigureScreens)
            return;

        if (!HasAccess(ent, actor))
        {
            _popup.PopupEntity(Loc.GetString("comms-console-permission-denied"), ent, actor, PopupType.Medium);
            return;
        }

        ent.Comp.LastConfiguredContent = args.Content;
        ent.Comp.LastConfiguredShowBorders = args.ShowBorder;
        ent.Comp.LastConfiguredLine1 = args.Line1;
        ent.Comp.LastConfiguredLine2 = args.Line2;
        Dirty(ent);
        var grid = Transform(ent).GridUid;
        if (grid is null)
            return;

        _deviceNetwork.QueuePacket(ent, null, new NetworkPayload
        {
            [ScreenPackets.Content] = args.Content,
            [ScreenPackets.Grid] = grid.Value,
            [ScreenPackets.ShowBorders] = args.ShowBorder,
            [ScreenPackets.Text] = (args.Line1, args.Line2),
        });
    }

    private void OnUiOpened(Entity<OnyxCommunicationsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshStationState(ent);
    }

    private EntityUid? RefreshStationState(Entity<OnyxCommunicationsConsoleComponent> ent)
    {
        ent.Comp.AlertLevels.Clear();
        if (_station.GetOwningStation(ent) is not { } station || !TryComp<AlertLevelComponent>(station, out var alert))
        {
            ent.Comp.CurrentAlertLevel = string.Empty;
            ent.Comp.CanSetAlertAt = null;
            Dirty(ent);
            return null;
        }

        ent.Comp.CurrentAlertLevel = alert.CurrentAlertLevel;
        ent.Comp.CanSetAlertAt = alert.IsLevelLocked ? null : alert.DelayedUntil ?? _timing.CurTime;
        foreach (var level in _alertLevel.GetSelectableAlertLevels((station, alert)))
        {
            var prototype = _prototypes.Index(level);
            ent.Comp.AlertLevels.Add(new($"alert-level-{level}", $"alert-level-{level}-announcement", level, prototype.Selectable, prototype.Color));
        }

        Dirty(ent);
        return station;
    }

    private void OnAlertLevelChanged(ref AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<OnyxCommunicationsConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) != args.Station)
                continue;
            console.CurrentAlertLevel = args.AlertLevel;
            console.CanSetAlertAt = TryComp<AlertLevelComponent>(args.Station, out var alert) && !alert.IsLevelLocked
                ? alert.DelayedUntil ?? _timing.CurTime
                : null;
            Dirty(uid, console);
        }
    }

    private void OnAlertLevelDelayFinished(ref AlertLevelDelayFinishedEvent args)
    {
        var query = EntityQueryEnumerator<OnyxCommunicationsConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) is not { } station || !TryComp<AlertLevelComponent>(station, out var alert))
                continue;

            console.CanSetAlertAt = alert.IsLevelLocked ? null : alert.DelayedUntil ?? _timing.CurTime;
            Dirty(uid, console);
        }
    }

    private void OnRoundEndChanged(RoundEndSystemChangedEvent args)
    {
        var query = EntityQueryEnumerator<OnyxCommunicationsConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            console.ExpectedEvacuationArrival = _roundEnd.ExpectedCountdownEnd;
            console.ExpectedEvacuationDuration = _roundEnd.ExpectedShuttleLength;
            console.ShuttlesCallable = CanCallOrRecall();
            Dirty(uid, console);
        }
    }

    private bool HasAccess(Entity<OnyxCommunicationsConsoleComponent> console, EntityUid actor)
    {
        return !TryComp<AccessReaderComponent>(console, out var reader) || _access.IsAllowed(actor, console, reader);
    }

    private bool CanCallOrRecall()
    {
        if (_emergencyShuttle.EmergencyShuttleArrived || !_roundEnd.CanCallOrRecall())
            return false;
        if (_roundEnd.ExpectedCountdownEnd == null)
            return true;
        if (_roundEnd.ShuttleTimeLeft is not { } left || _roundEnd.ExpectedShuttleLength is not { } length)
            return false;
        return left.TotalSeconds / length.TotalSeconds >= _config.GetCVar(CCVars.EmergencyRecallTurningPoint);
    }
}

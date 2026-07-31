using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Nuke;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Xenomorphs;
using Content.Shared._Onyx.Xenomorphs.Caste;
using Content.Shared._Onyx.Xenomorphs.Xenomorph;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nuke;
using Content.Shared.Station.Components;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Xenomorphs.GameTicking.Rules;

public sealed partial class XenomorphsRuleSystem : GameRuleSystem<XenomorphsRuleComponent>
{
    private static readonly EntProtoId XenomorphSpawnerProto = "SpawnPointGhostXenomorph";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NukeCodePaperSystem _nukeCodePaper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphsRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagEntitySelected);
        SubscribeLocalEvent<XenomorphComponent, ComponentInit>(OnXenomorphInit);
        SubscribeLocalEvent<XenomorphComponent, BeforeXenomorphEvolutionEvent>(BeforeXenomorphEvolution);
        SubscribeLocalEvent<XenomorphComponent, AfterXenomorphEvolutionEvent>(AfterXenomorphEvolution);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);
    }

    private void AfterAntagEntitySelected(
        EntityUid uid,
        XenomorphsRuleComponent component,
        ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null || !Exists(args.EntityUid))
            return;

        component.Xenomorphs.Add(args.EntityUid);
    }

    private void OnXenomorphInit(EntityUid uid, XenomorphComponent component, ComponentInit args)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var xenomorphsRule, out _))
        {
            if (!xenomorphsRule.Xenomorphs.Contains(uid))
                xenomorphsRule.Xenomorphs.Add(uid);
        }
    }

    private void BeforeXenomorphEvolution(
        EntityUid uid,
        XenomorphComponent component,
        BeforeXenomorphEvolutionEvent args)
    {
        if (!_protoManager.TryIndex(args.Caste, out var caste) || caste.MaxCount == 0)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var xenomorphsRule, out _))
        {
            if (!xenomorphsRule.Xenomorphs.Contains(uid))
                continue;

            if (GetXenomorphs(xenomorphsRule, args.Caste).Count >= caste.MaxCount
                || args.CheckNeedCasteDeath
                && caste.NeedCasteDeath != null
                && GetXenomorphs(xenomorphsRule, caste.NeedCasteDeath).Count > 0)
            {
                _popup.PopupEntity(
                    Loc.GetString("xenomorphs-evolution-no-cast-slot", ("caste", Loc.GetString(caste.Name))),
                    uid,
                    uid);
                args.Cancel();
                return;
            }
        }
    }

    private void AfterXenomorphEvolution(
        EntityUid uid,
        XenomorphComponent component,
        AfterXenomorphEvolutionEvent args)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var xenomorphsRule, out _))
        {
            if (xenomorphsRule.Xenomorphs.Remove(uid))
                xenomorphsRule.Xenomorphs.Add(args.EvolvedInto);
        }
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        if (ev.OwningStation == null || !GetStationGrids().Contains(ev.OwningStation.Value))
            return;

        var endRound = false;
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var xenomorphs, out _))
        {
            xenomorphs.WinType = WinType.CrewMinor;
            xenomorphs.WinConditions.Add(WinCondition.NukeExplodedOnStation);
            ForceEndSelf(uid);
            endRound = true;
        }

        if (endRound)
            _roundEnd.EndRound();
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New is not GameRunLevel.PostRound)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var xenomorphs, out _))
        {
            OnRoundEnd(xenomorphs);
            ForceEndSelf(uid);
        }
    }

    private void OnRoundEnd(XenomorphsRuleComponent component)
    {
        if (component.WinType != WinType.XenoMinor)
            return;

        var centcomms = _emergencyShuttle.GetCentcommMaps();
        foreach (var xenomorph in GetXenomorphs(component))
        {
            var mapUid = Transform(xenomorph).MapUid;
            if (mapUid == null || !centcomms.Contains(mapUid.Value))
                continue;

            component.WinType = WinType.XenoMajor;
            component.WinConditions.Add(WinCondition.XenoInfiltratedOnCentCom);
            break;
        }

        var stationGrids = GetStationGrids();
        var nukeQuery = AllEntityQuery<NukeComponent, TransformComponent>();
        while (nukeQuery.MoveNext(out var nuke, out var xform))
        {
            if (nuke.Status != NukeStatus.ARMED
                || xform.GridUid == null
                || !stationGrids.Contains(xform.GridUid.Value))
                continue;

            component.WinType = WinType.CrewMinor;
            component.WinConditions.Add(WinCondition.NukeActiveInStation);
            break;
        }
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        XenomorphsRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString($"xenomorphs-{component.WinType.ToString().ToLowerInvariant()}"));

        foreach (var condition in component.WinConditions)
            args.AddLine(Loc.GetString($"xenomorphs-cond-{condition.ToString().ToLowerInvariant()}"));
    }

    protected override void Started(
        EntityUid uid,
        XenomorphsRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextCheck = _timing.CurTime + component.CheckDelay;
    }

    protected override void ActiveTick(
        EntityUid uid,
        XenomorphsRuleComponent component,
        GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextCheck > _timing.CurTime)
            return;

        component.NextCheck = _timing.CurTime + component.CheckDelay;

        if (!component.AnnouncementTime.HasValue && GetXenomorphs(component, "Queen").Count > 0)
        {
            component.AnnouncementTime = _timing.CurTime
                + _random.Next(component.MinTimeToAnnouncement, component.MaxTimeToAnnouncement);
        }

        if (!component.Announced && component.AnnouncementTime <= _timing.CurTime)
        {
            component.Announced = true;

            if (!string.IsNullOrEmpty(component.Announcement))
            {
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString(component.Announcement),
                    component.Sender != null ? Loc.GetString(component.Sender) : null,
                    colorOverride: component.AnnouncementColor);
            }

            _audio.PlayGlobal(component.XenomorphInfestationSound, Filter.Broadcast(), true);
        }

        CheckRoundEnd(uid, component, gameRule);
    }

    private void CheckRoundEnd(EntityUid uid, XenomorphsRuleComponent component, GameRuleComponent gameRule)
    {
        var stationGrids = GetStationGrids();
        var humans = GetHumans(stationGrids);
        var xenomorphs = GetXenomorphs(component);

        var hasXenomorphSpawners = false;
        var spawnerQuery = AllEntityQuery<GhostRoleComponent, MetaDataComponent>();
        while (spawnerQuery.MoveNext(out _, out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID is not { } prototypeId || prototypeId != XenomorphSpawnerProto)
                continue;

            hasXenomorphSpawners = true;
            break;
        }

        if (xenomorphs.Count == 0 && !hasXenomorphSpawners)
        {
            if (component.Announced && !string.IsNullOrEmpty(component.NoMoreThreatAnnouncement))
            {
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString(component.NoMoreThreatAnnouncement),
                    component.Sender != null ? Loc.GetString(component.Sender) : null,
                    colorOverride: component.NoMoreThreatAnnouncementColor);
            }

            component.WinType = WinType.CrewMajor;
            component.WinConditions.Add(WinCondition.AllReproduceXenoDead);
            ForceEndSelf(uid, gameRule);
            return;
        }

        var allHumans = GetHumans(stationGrids, true);
        if (xenomorphs.Count > 0 && allHumans.Count == 0)
        {
            component.WinType = WinType.XenoMajor;
            component.WinConditions.Add(WinCondition.AllCrewDead);
            ForceEndSelf(uid, gameRule);
            _roundEnd.EndRound();
            return;
        }

        if (!component.Announced
            || component.WinType == WinType.XenoMinor
            || xenomorphs.Count / (float) (xenomorphs.Count + humans.Count)
            < component.XenomorphsShuttleCallPercentage)
            return;

        _roundEnd.DoRoundEndBehavior(
            RoundEndBehavior.ShuttleCall,
            component.ShuttleCallTime,
            component.RoundEndTextSender,
            component.RoundEndTextShuttleCall,
            component.RoundEndTextAnnouncement);
        _audio.PlayGlobal(component.XenomorphTakeoverSound, Filter.Broadcast(), true);

        component.WinType = WinType.XenoMinor;
        component.WinConditions.Add(WinCondition.XenoTakeoverStation);

        var station = _station.GetStations().FirstOrNull();
        if (station.HasValue)
            _nukeCodePaper.SendNukeCodes(station.Value);
    }

    private List<EntityUid> GetHumans(HashSet<EntityUid>? stationGrids = null, bool includeOffStation = false)
    {
        var humans = new List<EntityUid>();
        stationGrids ??= GetStationGrids();

        var players = AllEntityQuery<HumanoidProfileComponent, ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState)
                || !includeOffStation && !stationGrids.Contains(xform.GridUid ?? EntityUid.Invalid))
                continue;

            humans.Add(uid);
        }

        return humans;
    }

    private List<EntityUid> GetXenomorphs(
        XenomorphsRuleComponent xenomorphsRule,
        ProtoId<XenomorphCastePrototype>? caste = null)
    {
        var xenomorphs = new List<EntityUid>();

        foreach (var xenomorph in xenomorphsRule.Xenomorphs.ToList())
        {
            if (!Exists(xenomorph) || !TryComp<XenomorphComponent>(xenomorph, out var xenomorphComponent))
            {
                xenomorphsRule.Xenomorphs.Remove(xenomorph);
                continue;
            }

            if (_mobState.IsDead(xenomorph)
                || caste.HasValue && xenomorphComponent.Caste != caste.Value)
                continue;

            xenomorphs.Add(xenomorph);
        }

        return xenomorphs;
    }

    private HashSet<EntityUid> GetStationGrids()
    {
        var stationGrids = new HashSet<EntityUid>();
        foreach (var station in _station.GetStations())
        {
            if (HasComp<StationDataComponent>(station) && _station.GetLargestGrid(station) is { } grid)
                stationGrids.Add(grid);
        }

        return stationGrids;
    }
}

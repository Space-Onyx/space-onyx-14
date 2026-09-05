using Content.Server.Chat.Systems;
using Content.Shared.Maps;
using Content.Shared.Prototypes;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Weather;

public sealed partial class WeatherSchedulerSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private ChatSystem _chat = default!;

    private static readonly string[] WeatherStationCallsigns =
    [
        "lavaland-weather-station-callsign-whiskey",
        "lavaland-weather-station-callsign-bravo",
        "lavaland-weather-station-callsign-charlie",
        "lavaland-weather-station-callsign-foxtrot",
        "lavaland-weather-station-callsign-kilo",
        "lavaland-weather-station-callsign-lima",
        "lavaland-weather-station-callsign-mike",
        "lavaland-weather-station-callsign-oscar",
        "lavaland-weather-station-callsign-tango",
        "lavaland-weather-station-callsign-yankee",
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeatherSchedulerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WeatherSchedulerComponent, MapComponent>();
        while (query.MoveNext(out var uid, out var scheduler, out _))
        {
            if (!scheduler.SchedulerActive || scheduler.NextTransition > _timing.CurTime)
                continue;

            scheduler.CurrentStage = scheduler.Random
                ? PickRandomStage(scheduler.Stages)
                : (scheduler.CurrentStage + 1) % scheduler.Stages.Count;

            ApplyStage(uid, scheduler);
        }
    }

    private void OnMapInit(Entity<WeatherSchedulerComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<MapComponent>(ent) || !Validate(ent))
            return;

        ent.Comp.CurrentStage = 0;
        ent.Comp.SchedulerActive = true;
        ent.Comp.AnnouncementSender = Loc.GetString(
            "lavaland-weather-station-sender",
            ("callsign", Loc.GetString(_random.Pick(WeatherStationCallsigns))),
            ("number", _random.Next(1, 1000).ToString("D3")));
        ApplyStage(ent, ent.Comp);
    }

    private bool Validate(Entity<WeatherSchedulerComponent> ent)
    {
        if (ent.Comp.Stages.Count == 0)
            return Fail(ent, "requires at least one stage");

        for (var i = 0; i < ent.Comp.Stages.Count; i++)
        {
            var stage = ent.Comp.Stages[i];
            if (stage.Duration.Min < 0f || stage.Duration.Max < stage.Duration.Min)
                return Fail(ent, $"stage {i} has an invalid duration range");

            if (ent.Comp.Random && stage.Weight <= 0f)
                return Fail(ent, $"stage {i} must have a positive weight");

            if (stage.Weather is not { } weather)
                continue;

            if (!_prototypes.TryIndex<EntityPrototype>(weather, out var prototype) ||
                !prototype.HasComponent<WeatherStatusEffectComponent>(_componentFactory))
            {
                return Fail(ent, $"stage {i} references invalid weather prototype '{weather}'");
            }
        }

        return true;
    }

    private bool Fail(Entity<WeatherSchedulerComponent> ent, string message)
    {
        Log.Error($"Weather scheduler on {ToPrettyString(ent)} {message}.");
        ent.Comp.SchedulerActive = false;
        return false;
    }

    private int PickRandomStage(List<WeatherSchedulerStage> stages)
    {
        var totalWeight = 0f;
        foreach (var stage in stages)
        {
            totalWeight += stage.Weight;
        }

        var remaining = _random.NextFloat(totalWeight);
        for (var i = 0; i < stages.Count; i++)
        {
            remaining -= stages[i].Weight;
            if (remaining <= 0f)
                return i;
        }

        return stages.Count - 1;
    }

    private void ApplyStage(EntityUid mapUid, WeatherSchedulerComponent scheduler)
    {
        var stage = scheduler.Stages[scheduler.CurrentStage];
        var duration = _random.NextFloat(stage.Duration.Min, stage.Duration.Max);

        _weather.TrySetWeather(Transform(mapUid).MapID, stage.Weather, out _);
        if (stage.Announcement is { } announcement)
        {
            _chat.DispatchFilteredAnnouncement(
                Filter.BroadcastMap(Transform(mapUid).MapID),
                Loc.GetString(announcement),
                mapUid,
                scheduler.AnnouncementSender,
                colorOverride: Color.FromHex("#f72f2f"));
        }

        scheduler.NextTransition = _timing.CurTime + TimeSpan.FromSeconds(duration);
        Dirty(mapUid, scheduler);
    }
}

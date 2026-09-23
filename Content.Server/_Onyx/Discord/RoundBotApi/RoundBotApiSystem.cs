using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;

namespace Content.Server._Onyx.Discord.RoundBotApi;

public sealed partial class RoundBotApiSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private ServerGameTicker _gameTicker = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private int _apiTimeout;
    private bool _enabled;
    private bool _pushInProgress;
    private bool _pushQueued;
    private readonly object _pushLock = new();
    private RoundBotApiEventRequest? _pendingEvent;

    internal const string LobbyEvent = "lobby";
    internal const string StartedEvent = "started";
    internal const string EndedEvent = "ended";

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("round.api");
        _cfg.OnValueChanged(CCVars.DiscordRoundBotApiTimeoutSeconds, OnApiTimeoutChanged, true);
        _cfg.OnValueChanged(CCVars.DiscordRoundBotApiEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCVars.DiscordRoundBotApiUrl, OnApiUrlChanged, true);
        _cfg.OnValueChanged(CCVars.DiscordRoundBotApiToken, OnApiTokenChanged, true);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCVars.DiscordRoundBotApiEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(CCVars.DiscordRoundBotApiUrl, OnApiUrlChanged);
        _cfg.UnsubValueChanged(CCVars.DiscordRoundBotApiToken, OnApiTokenChanged);
        _cfg.UnsubValueChanged(CCVars.DiscordRoundBotApiTimeoutSeconds, OnApiTimeoutChanged);
        _httpClient.Dispose();
        base.Shutdown();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.PreRoundLobby:
                PushEvent(BuildEventRequest(LobbyEvent, null));
                break;
            case GameRunLevel.InRound:
                PushEvent(BuildEventRequest(StartedEvent, null));
                break;
            case GameRunLevel.PostRound:
                PushEvent(BuildEventRequest(EndedEvent, _gameTicker.RoundDuration()));
                break;
        }
    }

    private RoundBotApiEventRequest BuildEventRequest(string type, TimeSpan? duration)
    {
        var preset = _gameTicker.CurrentPreset;
        var shown = _gameTicker.Decoy ?? preset;
        return new RoundBotApiEventRequest(
            type,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            _gameMapManager.GetSelectedMap()?.MapName,
            shown == null ? preset?.ID : Loc.GetString(shown.ModeTitle),
            preset?.ID,
            _playerManager.PlayerCount,
            duration == null ? null : (long) duration.Value.TotalSeconds);
    }

    private void PushEvent(RoundBotApiEventRequest request)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_apiUrl) || string.IsNullOrWhiteSpace(_apiToken))
            return;

        lock (_pushLock)
        {
            if (_pushInProgress)
            {
                _pendingEvent = request;
                _pushQueued = true;
                return;
            }

            _pushInProgress = true;
        }

        _ = RunPushAsync(request);
    }

    private async Task RunPushAsync(RoundBotApiEventRequest request)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_apiTimeout));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Warning($"Round API event push returned bad status code: {response.StatusCode}");
            }
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("Round API event push timed out");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Round API event push failed: {e.Message}");
        }
        finally
        {
            FinishPush();
        }
    }

    private void FinishPush()
    {
        RoundBotApiEventRequest? queued = null;
        lock (_pushLock)
        {
            _pushInProgress = false;
            if (_pushQueued)
            {
                _pushQueued = false;
                queued = _pendingEvent;
                _pendingEvent = null;
            }
        }

        if (queued != null)
            _taskManager.RunOnMainThread(() => PushEvent(queued));
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
    }

    private void OnApiUrlChanged(string value)
    {
        _apiUrl = value;
    }

    private void OnApiTokenChanged(string value)
    {
        _apiToken = value;
    }

    private void OnApiTimeoutChanged(int value)
    {
        _apiTimeout = Math.Max(1, value);
    }

    private sealed record RoundBotApiEventRequest(
        [property: JsonPropertyName("type")]
        string Type,
        [property: JsonPropertyName("serverName")]
        string ServerName,
        [property: JsonPropertyName("roundId")]
        int RoundId,
        [property: JsonPropertyName("mapName")]
        string? MapName,
        [property: JsonPropertyName("preset")]
        string? Preset,
        [property: JsonPropertyName("presetId")]
        string? PresetId,
        [property: JsonPropertyName("playerCount")]
        int PlayerCount,
        [property: JsonPropertyName("durationSeconds")]
        long? DurationSeconds);
}

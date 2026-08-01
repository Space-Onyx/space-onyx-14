using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Onyx.Administration;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared._Onyx.Discord;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Onyx.Discord;

public sealed partial class ServerDiscordIdManager : EntitySystem
{
    private static readonly TimeSpan LinkCodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly HttpClient BotApiClient = new();

    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _net.RegisterNetMessage<MsgDiscordIdInfo>(OnDiscordIdInfoRequest);
        _net.RegisterNetMessage<MsgDiscordUnlinkRequest>(OnDiscordUnlinkRequest);
    }

    private async void OnDiscordIdInfoRequest(MsgDiscordIdInfo msg)
    {
        await SendDiscordInfo(msg.MsgChannel);
    }

    private async Task SendDiscordInfo(INetChannel channel)
    {
        var userId = channel.UserId;
        var discordId = _cfg.GetCVar(CCVars.DiscordAuthEnable)
            ? await _db.GetDiscordIdAsync(userId.UserId)
            : null;
        string? discordUsername = null;
        string? linkCode = null;

        if (discordId != null && ulong.TryParse(discordId, out var discordUserId))
        {
            try
            {
                discordUsername = await AuthApiHelper.GetAccountDiscord(
                    discordUserId,
                    _cfg.GetCVar(CCVars.DiscordTokenBot));
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to fetch Discord username for {discordId}: {exception}");
            }
        }

        if (discordId == null && _cfg.GetCVar(CCVars.DiscordAuthEnable) &&
            _players.TryGetSessionById(userId, out var session))
        {
            linkCode = await _db.GetOrCreateDiscordLinkCodeAsync(userId.UserId, session.Name, LinkCodeLifetime);
        }
        else if (discordId != null)
        {
            await _db.RemoveDiscordLinkCodeAsync(userId.UserId);
        }

        _net.ServerSendMessage(new MsgDiscordIdInfo
        {
            UserId = userId,
            DiscordId = discordId,
            DiscordUsername = discordUsername,
            LinkCode = linkCode
        }, channel);
    }

    private async Task<(bool Success, string Message)> RequestGlobalUnlinkAsync(Guid userId, string discordId)
    {
        var url = _cfg.GetCVar(CCVars.DiscordAuthBotApiUrl);
        var token = _cfg.GetCVar(CCVars.DiscordAuthBotApiToken);
        var timeoutSeconds = Math.Clamp(_cfg.GetCVar(CCVars.DiscordAuthBotApiTimeoutSeconds), 1, 30);

        if (string.IsNullOrWhiteSpace(url))
            return (false, "discord.auth_bot_api_url is empty.");

        if (string.IsNullOrWhiteSpace(token))
            return (false, "discord.auth_bot_api_token is empty.");

        var body = JsonSerializer.Serialize(new GlobalUnlinkRequestBody
        {
            UserId = userId.ToString(),
            DiscordId = discordId
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var response = await BotApiClient.SendAsync(request, timeout.Token);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, $"HTTP {(int) response.StatusCode}: {responseBody}");

            if (string.IsNullOrWhiteSpace(responseBody))
                return (true, "OK");

            try
            {
                var result = JsonSerializer.Deserialize<GlobalUnlinkResponseBody>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result is null || result.Ok
                    ? (true, result?.Message ?? "OK")
                    : (false, result.Message ?? "Global unlink failed.");
            }
            catch (JsonException)
            {
                return (true, "OK");
            }
        }
        catch (OperationCanceledException)
        {
            return (false, $"Request timeout after {timeoutSeconds}s.");
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }

    private async void OnDiscordUnlinkRequest(MsgDiscordUnlinkRequest msg)
    {
        if (!_cfg.GetCVar(CCVars.DiscordAuthEnable))
        {
            await SendDiscordInfo(msg.MsgChannel);
            return;
        }

        var userId = msg.MsgChannel.UserId;
        var discordId = await _db.GetDiscordIdAsync(userId.UserId);
        if (string.IsNullOrWhiteSpace(discordId))
        {
            await SendDiscordInfo(msg.MsgChannel);
            return;
        }

        var (success, message) = await RequestGlobalUnlinkAsync(userId.UserId, discordId);
        if (!success)
        {
            Log.Error($"Failed to globally unlink Discord for {userId}: {message}");
            await SendDiscordInfo(msg.MsgChannel);
            return;
        }

        await SendDiscordInfo(msg.MsgChannel);

        if (_cfg.GetCVar(CCVars.DiscordAuthLinkRequired))
            _net.DisconnectChannel(msg.MsgChannel, "Отвязка дискорд аккаунта.");

        Log.Info($"Discord account globally unlinked for {userId}. {message}");
    }

    private sealed class GlobalUnlinkRequestBody
    {
        [JsonPropertyName("user_id")]
        public required string UserId { get; init; }

        [JsonPropertyName("discord_id")]
        public required string DiscordId { get; init; }
    }

    private sealed class GlobalUnlinkResponseBody
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}

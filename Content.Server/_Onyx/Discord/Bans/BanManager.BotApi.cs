using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Database;

namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    private readonly HttpClient _banBotApiClient = new();

    private string _banBotApiUrl = string.Empty;
    private string _banBotApiToken = string.Empty;
    private int _banBotApiTimeout = 5;
    private bool _banBotApiEnabled;

    private void InitializeBanBotApi()
    {
        _cfg.OnValueChanged(CCVars.DiscordBanBotApiEnabled, value => _banBotApiEnabled = value, true);
        _cfg.OnValueChanged(CCVars.DiscordBanBotApiUrl, value => _banBotApiUrl = value.Trim(), true);
        _cfg.OnValueChanged(CCVars.DiscordBanBotApiToken, value => _banBotApiToken = value.Trim(), true);
        _cfg.OnValueChanged(CCVars.DiscordBanBotApiTimeoutSeconds, value => _banBotApiTimeout = Math.Max(1, value), true);
    }

    private async Task SendBanBotApi(BanDef banDef, BanWebhookPayload payload)
    {
        if (!_banBotApiEnabled
            || string.IsNullOrWhiteSpace(_banBotApiUrl)
            || string.IsNullOrWhiteSpace(_banBotApiToken)
            || payload.Embeds.Count == 0)
            return;

        try
        {
            var embed = payload.Embeds[0];
            var request = new BanBotApiEventRequest(
                "ban",
                _banWebhookServerName,
                banDef.Type.ToString(),
                banDef.ExpirationTime == null,
                banDef.Id,
                embed.Author.Name,
                embed.Description,
                embed.Color,
                embed.Footer.Text);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_banBotApiTimeout));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _banBotApiUrl)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _banBotApiToken);

            using var response = await _banBotApiClient.SendAsync(httpRequest, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Warning($"Ban bot API event push returned bad status code: {response.StatusCode}");
            }
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("Ban bot API event push timed out");
        }
        catch (Exception exception)
        {
            _sawmill.Warning($"Ban bot API event push failed: {exception.Message}");
        }
    }

    private sealed record BanBotApiEventRequest(
        [property: JsonPropertyName("type")]
        string Type,
        [property: JsonPropertyName("serverName")]
        string ServerName,
        [property: JsonPropertyName("banKind")]
        string BanKind,
        [property: JsonPropertyName("permanent")]
        bool Permanent,
        [property: JsonPropertyName("banId")]
        int? BanId,
        [property: JsonPropertyName("title")]
        string Title,
        [property: JsonPropertyName("description")]
        string Description,
        [property: JsonPropertyName("color")]
        int Color,
        [property: JsonPropertyName("footer")]
        string Footer);
}

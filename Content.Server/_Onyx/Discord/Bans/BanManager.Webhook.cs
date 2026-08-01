using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    private static readonly Regex BanWebhookRegex = new(@"^https://discord\.com/api/webhooks/(\d+)/((?!.*\/).*)$");
    private readonly HttpClient _banWebhookClient = new();
    private string _banWebhookServerName = string.Empty;
    private string _banWebhookUrl = string.Empty;

    private void InitializeBanWebhook()
    {
        _cfg.OnValueChanged(CCVars.DiscordBanWebhook, OnBanWebhookChanged, true);
        _cfg.OnValueChanged(CCVars.GameHostName, OnBanWebhookServerNameChanged, true);
    }

    private async Task SendBanWebhook(BanDef banDef)
    {
        if (string.IsNullOrWhiteSpace(_banWebhookUrl))
            return;

        try
        {
            var payload = banDef.Type == BanType.Role
                ? await GenerateRoleBanPayload(banDef)
                : await GenerateServerBanPayload(banDef);
            using var response = await _banWebhookClient.PostAsync(
                $"{_banWebhookUrl}?wait=true",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Error(
                    $"Discord returned bad status code when posting ban webhook: {response.StatusCode}\nResponse: {content}");
                return;
            }

            if (JsonNode.Parse(content)?["id"] == null)
                _sawmill.Warning($"Could not find message id in Discord ban webhook response: {content}");
        }
        catch (Exception exception)
        {
            _sawmill.Error($"Failed to send ban webhook: {exception}");
        }
    }

    private async Task<BanWebhookPayload> GenerateServerBanPayload(BanDef banDef)
    {
        var minutes = GetBanMinutes(banDef);
        var adminName = await GetBanAdminName(banDef.BanningAdmin);
        var targetName = await GetBanTargetName(banDef);
        var (adminMention, adminDiscordId) = await GetBanDiscordMention(banDef.BanningAdmin);
        NetUserId? targetUser = banDef.UserIds.Length == 0 ? null : banDef.UserIds[0];
        var (targetMention, targetDiscordId) = await GetBanDiscordMention(targetUser);
        var expiration = banDef.ExpirationTime is { } expires
            ? GetBanTimestamp(expires)
            : Loc.GetString("server-ban-string-never");

        return CreateBanPayload(
            banDef,
            minutes is { } value
                ? Loc.GetString("server-time-ban", ("mins", value))
                : Loc.GetString("server-perma-ban"),
            Loc.GetString(
                minutes == null ? "server-perma-ban-string" : "server-time-ban-string",
                ("targetName", targetName),
                ("targetDiscord", targetMention),
                ("adminName", adminName),
                ("adminDiscord", adminMention),
                ("TimeNow", GetBanTimestamp(banDef.BanTime)),
                ("expiresString", expiration),
                ("reason", banDef.Reason),
                ("severity", GetBanSeverity(banDef))),
            minutes == null ? 0x8B0000 : 0x803045,
            adminDiscordId,
            targetDiscordId);
    }

    private async Task<BanWebhookPayload> GenerateRoleBanPayload(BanDef banDef)
    {
        var minutes = GetBanMinutes(banDef);
        var adminName = await GetBanAdminName(banDef.BanningAdmin);
        var targetName = await GetBanTargetName(banDef);
        var (adminMention, adminDiscordId) = await GetBanDiscordMention(banDef.BanningAdmin);
        NetUserId? targetUser = banDef.UserIds.Length == 0 ? null : banDef.UserIds[0];
        var (targetMention, targetDiscordId) = await GetBanDiscordMention(targetUser);
        var roles = banDef.Roles is not { Length: > 0 }
            ? "\n> `-`"
            : string.Concat(banDef.Roles.Value.Select(role => $"\n> `{role}`"));
        var expiration = banDef.ExpirationTime is { } expires
            ? GetBanTimestamp(expires)
            : Loc.GetString("server-ban-string-never");

        return CreateBanPayload(
            banDef,
            minutes is { } value
                ? Loc.GetString("server-role-ban", ("mins", value))
                : Loc.GetString("server-perma-role-ban"),
            Loc.GetString(
                minutes == null ? "server-perma-role-ban-string" : "server-role-ban-string",
                ("targetName", targetName),
                ("targetDiscord", targetMention),
                ("adminName", adminName),
                ("adminDiscord", adminMention),
                ("TimeNow", GetBanTimestamp(banDef.BanTime)),
                ("roles", roles),
                ("expiresString", expiration),
                ("reason", banDef.Reason),
                ("severity", GetBanSeverity(banDef))),
            minutes == null ? 0xffb840 : 0x004281,
            adminDiscordId,
            targetDiscordId);
    }

    private BanWebhookPayload CreateBanPayload(
        BanDef banDef,
        string title,
        string description,
        int color,
        string? adminDiscordId,
        string? targetDiscordId)
    {
        var banId = banDef.Id is { } id ? $" #{id}" : string.Empty;
        var round = banDef.RoundIds.Length == 0 ? "-" : string.Join(", ", banDef.RoundIds);
        var mentions = new List<BanWebhookMention>();
        AddBanMention(mentions, adminDiscordId);
        AddBanMention(mentions, targetDiscordId);

        return new BanWebhookPayload
        {
            Mentions = mentions,
            Embeds =
            [
                new BanWebhookEmbed
                {
                    Description = description,
                    Color = color,
                    Author = new BanWebhookAuthor
                    {
                        Name = title + banId,
                        IconUrl = "https://cdn.discordapp.com/emojis/1129749368199712829.webp?size=40&quality=lossless",
                    },
                    Footer = new BanWebhookFooter
                    {
                        Text = Loc.GetString(
                            "server-ban-footer",
                            ("server", _banWebhookServerName[..Math.Min(_banWebhookServerName.Length, 1500)]),
                            ("round", round)),
                    },
                },
            ],
        };
    }

    private async Task<string> GetBanAdminName(NetUserId? userId)
    {
        if (userId == null)
            return Loc.GetString("system-user");

        return (await _db.GetPlayerRecordByUserId(userId.Value))?.LastSeenUserName ?? Loc.GetString("system-user");
    }

    private async Task<string> GetBanTargetName(BanDef banDef)
    {
        if (banDef.UserIds.Length == 0)
        {
            var hwid = banDef.HWIds.Length == 0 ? "null" : banDef.HWIds[0].ToString();
            return Loc.GetString("server-ban-no-name", ("hwid", hwid));
        }

        var names = new List<string>(banDef.UserIds.Length);
        foreach (var userId in banDef.UserIds)
            names.Add((await _db.GetPlayerRecordByUserId(userId))?.LastSeenUserName ?? userId.ToString());

        return string.Join(", ", names);
    }

    private async Task<(string Mention, string? DiscordId)> GetBanDiscordMention(NetUserId? userId)
    {
        if (userId == null)
            return (Loc.GetString("ban-webhook-no-discord"), null);

        var discordId = await _db.GetDiscordIdAsync(userId.Value.UserId);
        return string.IsNullOrWhiteSpace(discordId)
            ? (Loc.GetString("ban-webhook-no-discord"), null)
            : ($"<@{discordId}>", discordId);
    }

    private string GetBanSeverity(BanDef banDef)
    {
        return Loc.GetString($"admin-note-editor-severity-{banDef.Severity.ToString().ToLowerInvariant()}");
    }

    private static uint? GetBanMinutes(BanDef banDef)
    {
        if (banDef.ExpirationTime is not { } expires)
            return null;

        return (uint) Math.Max(1, Math.Round((expires - banDef.BanTime).TotalMinutes, MidpointRounding.AwayFromZero));
    }

    private static string GetBanTimestamp(DateTimeOffset time)
    {
        return time.ToLocalTime().ToString("g");
    }

    private void OnBanWebhookServerNameChanged(string serverName)
    {
        _banWebhookServerName = string.IsNullOrWhiteSpace(serverName) ? "Unknown Server" : serverName;
    }

    private void OnBanWebhookChanged(string url)
    {
        _banWebhookUrl = url.Trim();
        if (_banWebhookUrl != string.Empty &&
            !BanWebhookRegex.IsMatch(_banWebhookUrl))
        {
            _sawmill.Warning("discord.ban_webhook does not look like a valid Discord webhook URL.");
        }
    }

    private static void AddBanMention(List<BanWebhookMention> mentions, string? discordId)
    {
        if (!string.IsNullOrWhiteSpace(discordId))
            mentions.Add(new BanWebhookMention { Id = discordId });
    }

    private sealed class BanWebhookPayload
    {
        [JsonPropertyName("embeds")]
        public List<BanWebhookEmbed> Embeds { get; init; } = [];

        [JsonPropertyName("mentions")]
        public List<BanWebhookMention> Mentions { get; init; } = [];

        [JsonPropertyName("allowed_mentions")]
        public Dictionary<string, string[]> AllowedMentions { get; init; } =
            new() { { "parse", ["users"] } };
    }

    private sealed class BanWebhookEmbed
    {
        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("color")]
        public int Color { get; init; }

        [JsonPropertyName("author")]
        public required BanWebhookAuthor Author { get; init; }

        [JsonPropertyName("footer")]
        public required BanWebhookFooter Footer { get; init; }
    }

    private sealed class BanWebhookAuthor
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("icon_url")]
        public required string IconUrl { get; init; }
    }

    private sealed class BanWebhookFooter
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }

    private sealed class BanWebhookMention
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }
    }
}

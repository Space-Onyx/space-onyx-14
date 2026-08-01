using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Content.Server._Onyx.Administration;

public sealed partial class AuthApiHelper
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly ISawmill _sawmill = Logger.GetSawmill("auth-api");

    public static async Task<string> GetCreationDate(string uuid)
    {
        string url = $"https://auth.spacestation14.com/api/query/userid?userid={uuid}";

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Warning($"API request failed for UUID {uuid}: {response.StatusCode}");
                return "Дата не найдена";
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            using JsonDocument jsonDoc = JsonDocument.Parse(jsonResponse);

            if (jsonDoc.RootElement.TryGetProperty("createdTime", out JsonElement createdTimeElement) &&
                createdTimeElement.ValueKind != JsonValueKind.Null &&
                createdTimeElement.ValueKind != JsonValueKind.Undefined)
            {
                string? createdTimeStr = createdTimeElement.GetString();
                if (!string.IsNullOrEmpty(createdTimeStr))
                {
                    DateTimeOffset dateObj = DateTimeOffset.Parse(createdTimeStr);
                    return dateObj.ToString("dd.MM.yyyy");
                }
            }

            _sawmill.Warning($"CreatedTime property missing or invalid for UUID: {uuid}");
            return "Дата не найдена";
        }
        catch (HttpRequestException httpEx)
        {
            _sawmill.Warning($"HTTP error for UUID {uuid}: {httpEx.Message}");
            return "Ошибка соединения";
        }
        catch (JsonException jsonEx)
        {
            _sawmill.Warning($"JSON parsing error for UUID {uuid}: {jsonEx.Message}");
            return "Ошибка данных";
        }
        catch (FormatException)
        {
            _sawmill.Warning($"Invalid date format for UUID: {uuid}");
            return "Неверный формат даты";
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Unexpected error for UUID {uuid}: {ex.Message}");
            return "Ошибка системы";
        }
    }

    public static async Task<string?> GetAccountDiscord(ulong userId, string botToken)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            throw new InvalidOperationException("DISCORD_BOT_TOKEN not set.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var username = root.GetProperty("username").GetString();
        var discriminator = root.TryGetProperty("discriminator", out var property)
            ? property.GetString()
            : null;

        return discriminator is not null ? $"{username}#{discriminator}" : username;
    }

}

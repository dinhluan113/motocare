using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MotoCare.Api.Services;

public sealed record LocationOption(string Code, string Name);

public sealed class LocationService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IConfiguration configuration)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public Task<IReadOnlyList<LocationOption>> GetCountriesAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync("locations:countries", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await GetRestCountriesAsync(cancellationToken);
        });

    public Task<IReadOnlyList<LocationOption>> GetRegionsAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        return GetOrCreateAsync($"locations:regions:{normalizedCountry}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            if (normalizedCountry != "VN")
            {
                return [];
            }

            var client = httpClientFactory.CreateClient("VietnamLocations");
            var vietnamJson = await client.GetFromJsonAsync<JsonElement>("p/", cancellationToken);
            return ReadOptions(vietnamJson, "code", "name");
        });
    }

    public Task<IReadOnlyList<LocationOption>> GetAreasAsync(
        string countryCode,
        string regionCode,
        CancellationToken cancellationToken)
    {
        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        var normalizedRegion = regionCode.Trim();
        return GetOrCreateAsync(
            $"locations:areas:{normalizedCountry}:{normalizedRegion}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                if (normalizedCountry != "VN")
                {
                    return [];
                }

                var client = httpClientFactory.CreateClient("VietnamLocations");
                var vietnamJson = await client.GetFromJsonAsync<JsonElement>(
                    $"p/{Uri.EscapeDataString(normalizedRegion)}?depth=2",
                    cancellationToken);
                return vietnamJson.TryGetProperty("wards", out var wards)
                    ? ReadOptions(wards, "code", "name")
                    : [];
            });
    }

    private async Task<IReadOnlyList<LocationOption>> GetRestCountriesAsync(
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["RestCountries:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "REST Countries API key chưa được cấu hình trên máy chủ.");
        }

        var countries = new List<LocationOption>();
        var client = httpClientFactory.CreateClient("RestCountries");
        for (var offset = 0; ; offset += 100)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"?limit=100&offset={offset}&response_fields=names.common,names.translations,codes.alpha_2");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var hasMore = false;
            JsonElement items;
            if (json.ValueKind == JsonValueKind.Array)
            {
                items = json;
            }
            else if (json.TryGetProperty("data", out var data)
                     && data.TryGetProperty("objects", out var objects))
            {
                items = objects;
                hasMore = data.TryGetProperty("meta", out var meta)
                    && meta.TryGetProperty("more", out var more)
                    && more.ValueKind == JsonValueKind.True;
            }
            else
            {
                items = default;
            }

            if (items.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var page = items.EnumerateArray()
                .Select(ReadCountryOption)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray();
            countries.AddRange(page);
            if (!hasMore && page.Length < 100)
            {
                break;
            }
        }

        return countries
            .DistinctBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Name, StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo("vi-VN"),
                ignoreCase: true))
            .ToArray();
    }

    private static LocationOption? ReadCountryOption(JsonElement item)
    {
        var code = ReadNestedString(item, "codes", "alpha_2");
        var name = ReadNestedString(item, "names", "translations", "vie", "common");
        if (name.Length == 0)
        {
            name = ReadNestedString(item, "names", "native", "vie", "common");
        }
        if (name.Length == 0)
        {
            name = ReadNestedString(item, "names", "common");
        }

        return code.Length > 0 && name.Length > 0
            ? new LocationOption(code, name)
            : null;
    }

    private async Task<IReadOnlyList<LocationOption>> GetOrCreateAsync(
        string key,
        Func<ICacheEntry, Task<IReadOnlyList<LocationOption>>> factory)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<LocationOption>? cached) && cached is not null)
        {
            return cached;
        }

        using var entry = cache.CreateEntry(key);
        var value = await factory(entry);
        entry.Value = value;
        return value;
    }

    private static IReadOnlyList<LocationOption> ReadOptions(
        JsonElement json,
        string codeProperty,
        string nameProperty)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return json.EnumerateArray()
            .Select(item => new LocationOption(
                ReadAsString(item, codeProperty),
                ReadAsString(item, nameProperty)))
            .Where(item => item.Code.Length > 0 && item.Name.Length > 0)
            .OrderBy(item => item.Name, StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo("vi-VN"),
                ignoreCase: true))
            .ToArray();
    }

    private static string ReadAsString(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : value.ToString();
    }

    private static string ReadNestedString(JsonElement item, params string[] path)
    {
        var value = item;
        foreach (var property in path)
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(property, out value))
            {
                return string.Empty;
            }
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : value.ToString();
    }
}

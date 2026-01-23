using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PackageManager.Flatpak;

public class FlatpakHttpRequests
{
    
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://flathub.org/")
    };
    
    // Flathub v2 search: GET /api/v2/search?q=...
    public async Task<FlatpakApiResponse> SearchAsync(
        string query,
        int page = 1,
        int limit = 21,
        List<FlathubSearchFilter>? filters = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));

        var payload = new FlathubSearchRequest
        {
            Query = query,
            Page = page,
            HitsPerPage = limit,
            Filters = filters
        };

        var json = JsonSerializer.Serialize(payload, FlathubJsonContext.Default.FlathubSearchRequest);

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await Http.PostAsync("api/v2/search", content);

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var root = await JsonSerializer.DeserializeAsync(stream, FlathubJsonContext.Default.FlatpakApiResponse, ct)
            .ConfigureAwait(false);
            
        // AnsiConsole.MarkupLine($"[grey]Response JSON (first 500 chars):[/] {body.EscapeMarkup()}");

        return root ?? new FlatpakApiResponse { hits = new List<Hit>() };
    }
    
    public sealed class FlathubSearchRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("filters")]
        public List<FlathubSearchFilter>? Filters { get; set; }

        [JsonPropertyName("hits_per_page")]
        public int HitsPerPage { get; set; } = 21;

        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;
    }

    public sealed class FlathubSearchFilter
    {
        [JsonPropertyName("filter_type")]
        public string FilterType { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FlatpakHttpRequests.FlathubSearchRequest))]
[JsonSerializable(typeof(FlatpakHttpRequests.FlathubSearchFilter))]
[JsonSerializable(typeof(List<FlatpakHttpRequests.FlathubSearchFilter>))]
[JsonSerializable(typeof(Flatpak.FlatpakApiResponse))]
[JsonSerializable(typeof(Flatpak.Hit))]
[JsonSerializable(typeof(List<Flatpak.Hit>))]
[JsonSerializable(typeof(Flatpak.Translations))]
[JsonSerializable(typeof(Flatpak.FacetDistribution))]
[JsonSerializable(typeof(Flatpak.FacetStats))]
[JsonSerializable(typeof(Flatpak.AdditionalProp1))]
[JsonSerializable(typeof(Flatpak.AdditionalProp2))]
[JsonSerializable(typeof(Flatpak.AdditionalProp3))]
internal partial class FlathubJsonContext : JsonSerializerContext
{
}
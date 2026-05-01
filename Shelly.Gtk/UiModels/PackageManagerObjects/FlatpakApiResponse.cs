using System.Text.Json.Serialization;

namespace Shelly.Gtk.UiModels.PackageManagerObjects;

public class FlatpakApiResponse
{
    [JsonPropertyName("hits")] public List<Hit>? Hits { get; set; }

    [JsonPropertyName("query")] public string? Query { get; set; }

    [JsonPropertyName("processingTimeMs")] public int? ProcessingTimeMs { get; set; }

    [JsonPropertyName("hitsPerPage")] public int? HitsPerPage { get; set; }

    [JsonPropertyName("page")] public int? Page { get; set; }

    [JsonPropertyName("totalPages")] public int? TotalPages { get; set; }

    [JsonPropertyName("totalHits")] public int? TotalHits { get; set; }
}

public class Hit
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("app_id")] public string? AppId { get; set; }

    [JsonPropertyName("icon")] public string? Icon { get; set; }

    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("arches")] public List<string>? Arches { get; set; }
}
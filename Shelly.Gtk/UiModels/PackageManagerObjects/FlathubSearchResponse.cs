using System.Text.Json.Serialization;

namespace Shelly.Gtk.UiModels.PackageManagerObjects;

public class FlathubHit
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("app_id")] public string? AppId { get; set; }

    [JsonPropertyName("icon")] public string? Icon { get; set; }

    [JsonPropertyName("arches")] public List<string>? Arches { get; set; }
}

public class FlathubSearchResponse
{
    [JsonPropertyName("hits")] public List<FlathubHit>? Hits { get; set; }

    [JsonPropertyName("totalHits")] public int? TotalHits { get; set; }
}
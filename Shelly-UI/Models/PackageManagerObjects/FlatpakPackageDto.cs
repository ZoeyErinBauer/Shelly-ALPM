using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shelly_UI.Models.PackageManagerObjects;

public record FlatpakPackageDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Arch { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string LatestCommit {get; set;} = string.Empty;
    public string Summary { get; set; }  = string.Empty;
    public int Kind { get; init; }
    public string? IconPath { get; set; }
    public string Description { get; set; } = string.Empty;

    public List<AppstreamRelease> Releases { get; set; } = [];
    
    public List<string> Categories { get; set; } = [];
}

public record AppstreamRelease
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
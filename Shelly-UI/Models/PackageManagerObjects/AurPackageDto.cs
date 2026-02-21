using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shelly_UI.Models.PackageManagerObjects;

public record AurPackageDto()
{
    [JsonPropertyName("ID")]
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("PackageBaseID")]
    public int PackageBaseId { get; set; }

    [JsonPropertyName("PackageBase")]
    public string PackageBase { get; set; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("URL")]
    public string? Url { get; set; }

    [JsonPropertyName("NumVotes")]
    public int NumVotes { get; set; }

    [JsonPropertyName("Popularity")]
    public double Popularity { get; set; }

    [JsonPropertyName("OutOfDate")]
    public long? OutOfDate { get; set; }

    [JsonPropertyName("Maintainer")]
    public string? Maintainer { get; set; }

    [JsonPropertyName("FirstSubmitted")]
    public long FirstSubmitted { get; set; }

    [JsonPropertyName("LastModified")]
    public long LastModified { get; set; }

    [JsonPropertyName("URLPath")]
    public string? UrlPath { get; set; }

    [JsonPropertyName("Depends")]
    public List<string>? Depends { get; set; }

    [JsonPropertyName("MakeDepends")]
    public List<string>? MakeDepends { get; set; }

    [JsonPropertyName("OptDepends")]
    public List<string>? OptDepends { get; set; }

    [JsonPropertyName("CheckDepends")]
    public List<string>? CheckDepends { get; set; }

    [JsonPropertyName("Conflicts")]
    public List<string>? Conflicts { get; set; }

    [JsonPropertyName("Provides")]
    public List<string>? Provides { get; set; }

    [JsonPropertyName("Replaces")]
    public List<string>? Replaces { get; set; }

    [JsonPropertyName("Groups")]
    public List<string>? Groups { get; set; }

    [JsonPropertyName("License")]
    public List<string>? License { get; set; }

    [JsonPropertyName("Keywords")]
    public List<string>? Keywords { get; set; }
}
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PackageManager.Flatpak;

/// <summary>
/// Represents an application from Flatpak appstream metadata
/// </summary>
public class AppstreamApp
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("project_license")]
    public string ProjectLicense { get; set; } = string.Empty;

    [JsonPropertyName("developer_name")]
    public string DeveloperName { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("icons")]
    public List<AppstreamIcon> Icons { get; set; } = new();

    [JsonPropertyName("screenshots")]
    public List<AppstreamScreenshot> Screenshots { get; set; } = new();

    [JsonPropertyName("releases")]
    public List<AppstreamRelease> Releases { get; set; } = new();

    [JsonPropertyName("urls")]
    public Dictionary<string, string> Urls { get; set; } = new();

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("verification_method")]
    public string VerificationMethod { get; set; } = string.Empty;
}

/// <summary>
/// Represents an icon for an appstream application
/// </summary>
public class AppstreamIcon
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("scale")]
    public int Scale { get; set; } = 1;
}

/// <summary>
/// Represents a screenshot for an appstream application
/// </summary>
public class AppstreamScreenshot
{
    [JsonPropertyName("caption")]
    public string Caption { get; set; } = string.Empty;

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("images")]
    public List<AppstreamImage> Images { get; set; } = new();
}

/// <summary>
/// Represents an image in a screenshot
/// </summary>
public class AppstreamImage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

/// <summary>
/// Represents a release/version entry for an appstream application
/// </summary>
public class AppstreamRelease
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

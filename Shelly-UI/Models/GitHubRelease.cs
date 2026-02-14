using System.Text.Json.Serialization;

namespace Shelly_UI.Models;

public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("assets")] public GitHubAsset[] Assets { get; set; } = [];

    [JsonPropertyName("tarball_url")] public string TarballUrl { get; set; } = string.Empty;

    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
}

public class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}
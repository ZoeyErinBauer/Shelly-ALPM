using System.Text.Json.Serialization;

namespace Shelly_Notifications.Models;

public class SyncModel
{
    public List<SyncPackageModel> Packages { get; set; } = [];
    public List<SyncAurModel> Aur { get; set; } = [];
    public List<SyncFlatpakModel> Flatpaks { get; set; } = [];
}

public class SyncPackageModel
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DownloadSize { get; set; }
}

public class SyncAurModel
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldVersion { get; set; }
}

public class SyncFlatpakModel
{
    public string Id { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    public string Version { get; set; } = string.Empty;
}
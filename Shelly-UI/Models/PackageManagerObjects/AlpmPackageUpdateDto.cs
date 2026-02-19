namespace Shelly_UI.Models.PackageManagerObjects;

public record AlpmPackageUpdateDto()
{
    public string Name { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
    public long DownloadSize { get; init; }
}
using System.Collections.Generic;

namespace Shelly_UI.Models.PackageManagerObjects;

public record AlpmPackageDto
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long Size { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Repository { get; init; } = string.Empty;

    public List<string> Replaces { get; init; } = [];
}
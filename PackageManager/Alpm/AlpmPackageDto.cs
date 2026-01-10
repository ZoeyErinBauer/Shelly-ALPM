namespace PackageManager.Alpm;

public record AlpmPackageDto
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long Size { get; init; }
}

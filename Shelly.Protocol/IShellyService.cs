namespace Shelly.Protocol;

/// <summary>
/// Constants for the Shelly D-Bus service.
/// </summary>
public static class ShellyDbusConstants
{
    public const string ServiceName = "org.shelly.PackageManager";
    public const string ObjectPath = "/org/shelly/PackageManager1";
    public const string InterfaceName = "org.shelly.PackageManager1";
}

/// <summary>
/// Package information DTO for D-Bus transfer.
/// </summary>
public record PackageInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long Size { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
}

/// <summary>
/// Package update information DTO for D-Bus transfer.
/// </summary>
public record PackageUpdateInfo
{
    public string Name { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
    public long DownloadSize { get; init; }
}

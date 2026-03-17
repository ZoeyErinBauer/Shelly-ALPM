namespace PackageManager.Alpm;

public enum AlpmProgressType
{
    AddStart = 0,
    UpgradeStart,
    DowngradeStart,
    ReinstallStart,
    RemoveStart,
    ConflictsStart,
    DiskspaceStart,
    IntegrityStart,
    LoadStart,
    KeyringStart,
    PackageDownload = 100,
    DatabaseDownload = 101
}

using System;

namespace PackageManager.Alpm;

public class AlpmPackageUpdate(AlpmPackage installedPackage, AlpmPackage newPackage)
{
    public string Name => installedPackage.Name;
    public string CurrentVersion => installedPackage.Version;
    public string NewVersion => newPackage.Version;
    public long DownloadSize => newPackage.Size;
    public long SizeDifference => newPackage.Size - installedPackage.Size;

    public AlpmPackageUpdateDto ToDto() => new AlpmPackageUpdateDto
    {
        Name = Name,
        CurrentVersion = CurrentVersion,
        NewVersion = NewVersion,
        DownloadSize = DownloadSize
    };

    public override string ToString()
    {
        return $"Package: {Name}, Current: {CurrentVersion}, New: {NewVersion}, Download Size: {DownloadSize}, Difference: {SizeDifference}";
    }
}

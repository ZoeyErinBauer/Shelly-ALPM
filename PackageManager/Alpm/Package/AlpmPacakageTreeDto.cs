using System.Collections.Generic;

namespace PackageManager.Alpm.Package;

public record AlpmPackageTreeDto(string Name)
{
    public List<AlpmPackageTreeDto> Files { get; } = [];
}
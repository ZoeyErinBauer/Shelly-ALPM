using System.Collections.Generic;

namespace PackageManager.Alpm.Package;

public record AlpmPackageFileDto(string Name)
{
    private List<AlpmPackageFileDto> Files { get; set; } = [];
}
namespace Shelly.Gtk.UiModels.PackageManagerObjects;

public record AlpmPackageTreeDto(string Name)
{
    public List<AlpmPackageTreeDto> Files { get; init; } = [];
}

namespace PackageManager.Flatpak;

public class FlatpakPackageDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Arch { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string LatestCommit {get; set;} = string.Empty;
    public string Summary { get; set; }  = string.Empty;
    public int Kind { get; init; }
}
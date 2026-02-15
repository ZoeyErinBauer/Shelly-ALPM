namespace Shelly_CLI;

public class SyncModel
{
    public List<SyncPackageModel> Packages { get; set; } = [];
    public List<SyncAurModel> Aur { get; set; } = [];
    public List<SyncFlatpakModel> Flatpaks { get; set; } = [];
}

public class SyncPackageModel
{
    public string Name { get; set; } = string.Empty;
    
    public string Version { get; set; } = string.Empty;
}

public class SyncAurModel
{
    public string Name { get; set; } = string.Empty;
    
    public string Version { get; set; } = string.Empty;
}

public class SyncFlatpakModel
{
    public string Id { get; set; } = string.Empty;
    
    public string Version { get; set; } = string.Empty;
}
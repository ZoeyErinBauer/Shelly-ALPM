using System.Collections.Generic;

namespace PackageManager.Aur.Models;

public class VcsSourceEntry
{
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public List<string> Protocols { get; set; } = [];
    public string CommitSha { get; set; } = string.Empty;
}

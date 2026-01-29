using System.Collections.Generic;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace PackageManager.Alpm;

internal class AlpmRepo
{
    public string Name;                  // The name of the repository (e.g., "core", "extra")
    public AlpmSigLevel SigLevel;        // The signature level for this specific repo
    public AlpmSigLevel SigLevelMask;    // Mask for merging signature levels
    public AlpmDbUsage Usage;            // Usage flags (Sync, Search, Install, Upgrade)
    public List<string> Urls;            // List of mirror URLs for this repository

    public AlpmRepo(string name)
    {
        Name = name;
        SigLevel = AlpmSigLevel.UseDefault;
        Usage = 0;
        Urls = new List<string>();
    }
}
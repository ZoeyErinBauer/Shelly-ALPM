using System;

namespace PackageManager.Alpm;

[Flags]
internal enum AlpmDbUsage : int
{
    /// <summary>
    /// Enable refreshes (syncing) for this database.
    /// </summary>
    Sync = 1,

    /// <summary>
    /// Enable search for this database.
    /// </summary>
    Search = (1 << 1),

    /// <summary>
    /// Enable installing packages from this database.
    /// </summary>
    Install = (1 << 2),

    /// <summary>
    /// Enable system upgrades with this database.
    /// </summary>
    Upgrade = (1 << 3),

    /// <summary>
    /// Enable all usage levels.
    /// </summary>
    All = (1 << 4) - 1
}
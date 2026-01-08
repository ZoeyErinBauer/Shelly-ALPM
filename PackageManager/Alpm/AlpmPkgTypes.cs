namespace PackageManager.Alpm;

internal enum AlpmPkgReason : int
{
    Explicit = 0,
    Depend = 1,
    Unknown = 2
}

internal enum AlpmPkgFrom : int
{
    File = 1,
    LocalDb,
    SyncDb
}

[System.Flags]
internal enum AlpmPkgValidation : int
{
    Unknown = 0,
    None = (1 << 0),
    Md5Sum = (1 << 1),
    Sha256Sum = (1 << 2),
    Signature = (1 << 3)
}

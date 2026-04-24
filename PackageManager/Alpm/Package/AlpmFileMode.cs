namespace PackageManager.Alpm.Package;

public enum AlpmFileMode : uint
{
    NamedPipe   = 0x1000, // S_IFIFO
    CharDevice  = 0x2000, // S_IFCHR
    Directory   = 0x4000, // S_IFDIR
    BlockDevice = 0x6000, // S_IFBLK
    File        = 0x8000, // S_IFREG
    Symlink     = 0xA000, // S_IFLNK
    Socket      = 0xC000, // S_IFSOCK
}


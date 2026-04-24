namespace PackageManager.Alpm.Package;

public static class AlpmFileModeExtensions
{
    private const uint S_IFMT = 0xF000;

    public static AlpmFileMode GetFileType(this uint mode) =>
        (AlpmFileMode)(mode & S_IFMT);

    public static bool IsDirectory(this uint mode) =>
        mode.GetFileType() == AlpmFileMode.Directory;

    public static bool IsSymlink(this uint mode) =>
        mode.GetFileType() == AlpmFileMode.Symlink;

    public static uint GetPermissions(this uint mode) => mode & 0xFFFu;
}
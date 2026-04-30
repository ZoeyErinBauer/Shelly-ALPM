namespace Shelly.Utilities;

public static class SizeHelper
{
    public static string FormatSize(long bytes, SizeDisplay display = SizeDisplay.Auto)
    {
        string sign = bytes < 0 ? "-" : string.Empty;
        bytes = Math.Abs(bytes);
        return display switch
        {
            SizeDisplay.Auto => bytes switch
            {
                >= 1L << 40 => FormatTiB(bytes, sign),
                >= 1L << 30 => FormatGiB(bytes, sign),
                >= 1L << 20 => FormatMiB(bytes, sign),
                >= 1L << 10 => FormatKiB(bytes, sign),
                _ => $"{sign}{bytes} B"
            },
            SizeDisplay.Terabytes => FormatTiB(bytes, sign),
            SizeDisplay.Gigabytes => FormatGiB(bytes, sign),
            SizeDisplay.Megabytes => FormatMiB(bytes, sign),
            SizeDisplay.Kilobytes => FormatKiB(bytes, sign),
            SizeDisplay.Bytes => $"{bytes} B",
            _ => throw new ArgumentOutOfRangeException(nameof(display), display, "Unhandled SizeDisplay value")
        };
    }

    private static string FormatKiB(long bytes, string sign) => $"{sign}{bytes / (1024.0):F2} KiB";
    private static string FormatMiB(long bytes, string sign) => $"{sign}{bytes / (1024.0 * 1024.0):F2} MiB";
    private static string FormatGiB(long bytes, string sign) => $"{sign}{bytes / (1024.0 * 1024.0 * 1024.0):F2} GiB";
    private static string FormatTiB(long bytes, string sign) => $"{sign}{bytes / (1024.0 * 1024.0 * 1024.0 * 1024.0):F2} TiB";
}
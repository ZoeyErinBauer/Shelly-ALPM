namespace Shelly.Gtk.Services;

public static class DirtyScopes
{
    public const string All        = "all";

    // High-level groups (raise these from services)
    public const string Config     = "config";
    public const string Tray       = "tray";
    public const string News       = "news";
    public const string Aur        = "aur";
    public const string Flatpak    = "flatpak";
    public const string AppImage   = "appimage";
    public const string Native     = "native";

    // Fine-grained sub-scopes (optional)
    public const string AurInstalled     = "aur.installed";
    public const string AurUpdates       = "aur.updates";
    public const string FlatpakInstalled = "flatpak.installed";
    public const string FlatpakUpdates   = "flatpak.updates";
    public const string NativeInstalled  = "native.installed";
    public const string NativeUpdates    = "native.updates";

    /// <summary>Does <paramref name="raised"/> match what <paramref name="listening"/> cares about?</summary>
    public static bool Matches(string raised, params string[] listening)
    {
        if (raised == All) return true;
        foreach (var l in listening)
        {
            if (l == All) return true;
            if (l == raised) return true;
            // group match: "aur" matches "aur.installed", "aur.updates", ...
            if (raised.StartsWith(l + ".", StringComparison.Ordinal)) return true;
            if (l.StartsWith(raised + ".", StringComparison.Ordinal)) return true;
        }
        return false;
    }
}

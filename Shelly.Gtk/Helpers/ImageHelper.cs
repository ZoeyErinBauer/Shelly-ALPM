using Gtk;

namespace Shelly.Gtk.Helpers;

public static class ImageHelper
{
    public static string GetIconWithFallback(string fallbackName, params string[] iconNames)
    {
        var iconTheme = IconTheme.GetForDisplay(Gdk.Display.GetDefault()!);
        foreach (var iconName in iconNames)
        {
            if (iconTheme.HasIcon(iconName))
            {
                return iconName;
            }
        }

        return fallbackName;
    }
}

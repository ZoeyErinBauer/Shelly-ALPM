using Gtk;

namespace Shelly.Gtk.Helpers;

public static class OverlayHelper
{
    public static bool HasActiveOverlay(Widget widget)
    {
        var current = widget;
        while (current != null)
        {
            if (current is Overlay overlay)
            {
                if (overlay.GetFirstChild()?.GetNextSibling() != null)
                {
                    return true;
                }
            }
            current = current.GetParent();
        }
        return false;
    }
}

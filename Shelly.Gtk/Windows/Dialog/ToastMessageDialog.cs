using Gtk;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Windows.Dialog;

public static class ToastMessageDialog
{
    public static void ShowToastMessage(Overlay parentOverlay, ToastMessageEventArgs e)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            var toastFrame = new Frame();
            toastFrame.SetLabel(null);
            toastFrame.AddCssClass("background");
            toastFrame.AddCssClass("toast-message");
            toastFrame.SetOverflow(Overflow.Hidden);
            toastFrame.SetHalign(Align.Center);
            toastFrame.SetValign(Align.End);
            toastFrame.SetMarginBottom(40);

            var toastBox = Box.New(Orientation.Horizontal, 8);

            var label = Label.New(e.Title);
            label.SetMarginTop(5);
            label.SetMarginBottom(5);
            label.SetMarginStart(5);
            label.SetMarginEnd(5);

            toastBox.Append(label);
            toastFrame.SetChild(toastBox);

            parentOverlay.AddOverlay(toastFrame);

            GLib.Functions.TimeoutAdd(0, (uint)3000, () =>
            {
                if (toastFrame.GetParent() != null)
                {
                    parentOverlay.RemoveOverlay(toastFrame);
                }
                return false;
            });

            return false;
        });
    }
}
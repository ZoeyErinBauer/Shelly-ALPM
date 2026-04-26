using Gtk;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Windows.Dialog;

public static class GenericOverlay
{
    public static void ShowGenericOverlay(Overlay parentOverlay, Widget content, GenericDialogEventArgs e, int width = 400, int height = -1)
    {
        var backdrop = new Box();
        backdrop.SetOrientation(Orientation.Horizontal);
        backdrop.Hexpand = true;
        backdrop.Vexpand = true;
        backdrop.AddCssClass("lockout-overlay");

        var baseFrame = new Frame();
        baseFrame.SetLabel(null);
        baseFrame.SetHalign(Align.Center);
        baseFrame.SetValign(Align.Center);
        baseFrame.SetSizeRequest(width, height);
        baseFrame.SetMarginTop(20);
        baseFrame.SetMarginBottom(20);
        baseFrame.SetMarginStart(20);
        baseFrame.SetMarginEnd(20);
        baseFrame.AddCssClass("background");
        baseFrame.AddCssClass("dialog-overlay");
        baseFrame.SetOverflow(Overflow.Hidden);

        var baseBox = new Box();
        baseBox.SetOrientation(Orientation.Vertical);
        baseBox.SetSpacing(12);
        baseFrame.SetChild(baseBox);

        var grid = new Grid();
        grid.Hexpand = true;

        var spacer = new Box();
        spacer.Hexpand = true;

        var closeButton = new Button();
        closeButton.SetHalign(Align.End);
        closeButton.SetIconName("window-close-symbolic");

        grid.Attach(spacer,      0, 0, 1, 1);
        grid.Attach(closeButton, 1, 0, 1, 1);
        grid.Attach(content,     0, 1, 2, 1);

        baseBox.Append(grid);

        closeButton.OnClicked += (_, _) => Dismiss();

        var gestureClick = GestureClick.New();
        gestureClick.OnReleased += (_,  args) =>
        {
            backdrop.TranslateCoordinates(baseFrame, args.X, args.Y, out var x, out var y);

            var insideCard = x >= 0 && y >= 0
                           && x <= baseFrame.GetAllocatedWidth()
                           && y <= baseFrame.GetAllocatedHeight();

            if (!insideCard)
                Dismiss();
        };

        backdrop.AddController(gestureClick);
        backdrop.Append(baseFrame);

        parentOverlay.AddOverlay(backdrop);
        _ = e.ResponseTask.ContinueWith(_ =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                Dismiss();
                return false;
            });
        });
        return;

        void Dismiss()
        {
            if (e.ResponseTask.IsCompleted)
            {
                if (backdrop.Parent != null)
                {
                    parentOverlay.RemoveOverlay(backdrop);
                }
                return;
            }

            e.SetResponse(false);
            if (backdrop.Parent != null)
            {
                parentOverlay.RemoveOverlay(backdrop);
            }
        }
    }
}
using Gtk;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Windows.Dialog;

public static class PackageBuildDialog
{
    public static void ShowPackageBuildDialog(Overlay parentOverlay, PackageBuildEventArgs e)
    {
        var baseFrame = Frame.New(null);
        baseFrame.SetHalign(Align.Center);
        baseFrame.SetValign(Align.Center);
        baseFrame.SetSizeRequest(600, 500);
        baseFrame.SetMarginTop(20);
        baseFrame.SetMarginBottom(20);
        baseFrame.SetMarginStart(20);
        baseFrame.SetMarginEnd(20);
        baseFrame.AddCssClass("background");
        baseFrame.AddCssClass("dialog-overlay");
        baseFrame.SetOverflow(Overflow.Hidden);

        var box = Box.New(Orientation.Vertical, 12);
        baseFrame.SetChild(box);

        var titleLabel = Label.New(e.Title);
        titleLabel.AddCssClass("title-4");
        box.Append(titleLabel);

        var pkgBuildLabel = Label.New(e.PkgBuild);
        pkgBuildLabel.SetWrap(false);
        pkgBuildLabel.SetXalign(0);
        pkgBuildLabel.AddCssClass("monospace");

        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
        scrolledWindow.SetVexpand(true);
        scrolledWindow.SetChild(pkgBuildLabel);
        box.Append(scrolledWindow);

        var buttonBox = Box.New(Orientation.Horizontal, 8);
        buttonBox.SetHalign(Align.End);

        var cancelButton = Button.NewWithLabel("Cancel");
        var confirmButton = Button.NewWithLabel("Confirm");
        confirmButton.AddCssClass("suggested-action");

        cancelButton.OnClicked += (_,_) =>
        {
            e.SetResponse(false);
            parentOverlay.RemoveOverlay(baseFrame);
        };

        confirmButton.OnClicked += (_,_) =>
        {
            e.SetResponse(true);
            parentOverlay.RemoveOverlay(baseFrame);
        };

        buttonBox.Append(confirmButton);
        buttonBox.Append(cancelButton);
        box.Append(buttonBox);

        parentOverlay.AddOverlay(baseFrame);
    }
}

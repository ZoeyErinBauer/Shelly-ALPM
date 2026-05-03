using GObject;


namespace Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

[Subclass<GObject.Object>]
public partial class AlpmPackageGObject
{
    public int Index { get; set; } = -1;
    public bool IsSelected { get; set; }
    public bool IsInstalled { get; set; }

    public event EventHandler? OnSelectionToggled;

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        OnSelectionToggled?.Invoke(this, EventArgs.Empty);
    }
}

using GObject;

namespace Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

[Subclass<GObject.Object>]
public partial class MetaPackageGObject
{
    public MetaPackageModel? Package { get; set; }
    public bool IsSelected { get; set; }

    public bool IsInstalled
    {
        get => Package?.IsInstalled ?? false;
        set
        {
            if (Package == null) return;
            Package.IsInstalled = value;
            OnIsInstalledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? OnSelectionToggled;
    public event EventHandler? OnIsInstalledChanged;

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        OnSelectionToggled?.Invoke(this, EventArgs.Empty);
    }

    public void NotifySelectionChanged()
    {
        OnSelectionToggled?.Invoke(this, EventArgs.Empty);
    }
}
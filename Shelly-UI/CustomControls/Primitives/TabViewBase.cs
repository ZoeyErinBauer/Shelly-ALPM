using Avalonia;
using Avalonia.Controls;

namespace Shelly_UI.CustomControls.Primitives;

public class TabViewBase : TabControl
{
    protected virtual void OnSelectionChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
    }
}
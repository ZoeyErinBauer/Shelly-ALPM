using Avalonia.Controls;
using Avalonia.Media;

namespace Shelly_UI.CustomControls.Primitives;

public interface IMaterial
{
    public ExperimentalAcrylicMaterial Material { get; set; }
    public Color TintColor { get; set; }
    public double TintColorOpacity { get; set; }
    public double MaterialOpacity { get; set; }
    public bool MaterialIsVisible { get; set; }
}
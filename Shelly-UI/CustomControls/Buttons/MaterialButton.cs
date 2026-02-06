using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Shelly_UI.CustomControls.Primitives;

namespace Shelly_UI.CustomControls.Buttons;

public class MaterialButton : Button, IMaterial
{
    public ExperimentalAcrylicMaterial Material { get; set; }

    public Color TintColor
    {
        get => GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    private static readonly StyledProperty<Color> TintColorProperty =
        AvaloniaProperty.Register<MaterialButton, Color>(nameof(TintColor), Colors.White);


    public double TintColorOpacity
    {
        get => GetValue(TintColorOpacityProperty);
        set => SetValue(TintColorOpacityProperty, value);
    }

    private static readonly StyledProperty<double> TintColorOpacityProperty =
        AvaloniaProperty.Register<MaterialButton, double>(nameof(TintColorOpacity), 0.85);

    public double MaterialOpacity
    {
        get => GetValue(MaterialOpacityProperty);
        set => SetValue(MaterialOpacityProperty, value);
    }

    private static readonly StyledProperty<double> MaterialOpacityProperty =
        AvaloniaProperty.Register<MaterialButton, double>(nameof(MaterialOpacity), 0.85);

    public MaterialButton()
    {
        Material = new ExperimentalAcrylicMaterial()
        {
            TintColor = TintColor,
            TintOpacity = TintColorOpacity,
            MaterialOpacity = MaterialOpacity
        };
    }


    /// <summary>
    /// Defines if the Material can be visible
    /// </summary>
    public bool MaterialIsVisible
    {
        get => GetValue(MaterialIsVisibleProperty);
        set => SetValue(MaterialIsVisibleProperty, value);
    }

    private static readonly StyledProperty<bool> MaterialIsVisibleProperty =
        AvaloniaProperty.Register<MaterialButton, bool>(nameof(MaterialIsVisible), true);
}
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Material.Icons;

namespace Shelly_UI.CustomControls.Menus;

public partial class HeaderRow : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<HeaderRow, string>(nameof(Title));
    
    public static readonly StyledProperty<bool> ShowTitleButtonProperty =
        AvaloniaProperty.Register<HeaderRow, bool>(nameof(Title));

    public static readonly StyledProperty<MaterialIconKind> TitleIconProperty =
        AvaloniaProperty.Register<HeaderRow, MaterialIconKind>(nameof(TitleIcon));

    public static readonly StyledProperty<ICommand> TitleCommandProperty =
        AvaloniaProperty.Register<HeaderRow, ICommand>(nameof(TitleCommand));

    public static readonly StyledProperty<string> SearchWatermarkProperty =
        AvaloniaProperty.Register<HeaderRow, string>(nameof(SearchWatermark));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<HeaderRow, string>(nameof(SearchText));

    public static readonly StyledProperty<ICommand> SyncCommandProperty =
        AvaloniaProperty.Register<HeaderRow, ICommand>(nameof(SyncCommand));

    public static readonly StyledProperty<string> SyncTooltipProperty =
        AvaloniaProperty.Register<HeaderRow, string>(nameof(SyncTooltip));

    public static readonly StyledProperty<MaterialIconKind> SyncIconProperty =
        AvaloniaProperty.Register<HeaderRow, MaterialIconKind>(nameof(SyncIcon), MaterialIconKind.Sync);

    public static readonly StyledProperty<ICommand> SearchCommandProperty =
        AvaloniaProperty.Register<HeaderRow, ICommand>(nameof(SearchCommand));

    public static readonly StyledProperty<bool> ShowSearchBoxProperty =
        AvaloniaProperty.Register<HeaderRow, bool>(nameof(ShowSearchBox), true);

    public static readonly StyledProperty<bool> ShowComboBoxProperty =
        AvaloniaProperty.Register<HeaderRow, bool>(nameof(ShowComboBox), false);

    public static readonly StyledProperty<object> ComboBoxItemsSourceProperty =
        AvaloniaProperty.Register<HeaderRow, object>(nameof(ComboBoxItemsSource));

    public static readonly StyledProperty<object> ComboBoxSelectedItemProperty =
        AvaloniaProperty.Register<HeaderRow, object>(nameof(ComboBoxSelectedItem));

    public static readonly StyledProperty<int> ComboBoxSelectedIndexProperty =
        AvaloniaProperty.Register<HeaderRow, int>(nameof(ComboBoxSelectedIndex));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    
    public bool ShowTitleButton
    {
        get => GetValue(ShowTitleButtonProperty);
        set => SetValue(ShowTitleButtonProperty, value);
    }

    public MaterialIconKind TitleIcon
    {
        get => GetValue(TitleIconProperty);
        set => SetValue(TitleIconProperty, value);
    }

    public ICommand TitleCommand
    {
        get => GetValue(TitleCommandProperty);
        set => SetValue(TitleCommandProperty, value);
    }

    public string SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public ICommand SyncCommand
    {
        get => GetValue(SyncCommandProperty);
        set => SetValue(SyncCommandProperty, value);
    }

    public string SyncTooltip
    {
        get => GetValue(SyncTooltipProperty);
        set => SetValue(SyncTooltipProperty, value);
    }

    public MaterialIconKind SyncIcon
    {
        get => GetValue(SyncIconProperty);
        set => SetValue(SyncIconProperty, value);
    }

    public ICommand SearchCommand
    {
        get => GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public bool ShowSearchBox
    {
        get => GetValue(ShowSearchBoxProperty);
        set => SetValue(ShowSearchBoxProperty, value);
    }

    public bool ShowComboBox
    {
        get => GetValue(ShowComboBoxProperty);
        set => SetValue(ShowComboBoxProperty, value);
    }

    public object ComboBoxItemsSource
    {
        get => GetValue(ComboBoxItemsSourceProperty);
        set => SetValue(ComboBoxItemsSourceProperty, value);
    }

    public object ComboBoxSelectedItem
    {
        get => GetValue(ComboBoxSelectedItemProperty);
        set => SetValue(ComboBoxSelectedItemProperty, value);
    }

    public int ComboBoxSelectedIndex
    {
        get => GetValue(ComboBoxSelectedIndexProperty);
        set => SetValue(ComboBoxSelectedIndexProperty, value);
    }

    public HeaderRow()
    {
        InitializeComponent();
    }
}
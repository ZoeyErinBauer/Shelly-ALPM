using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Shelly_UI.Assets;

namespace Shelly_UI.Views;

public class ProviderSelectionDialog : Window
{
    private readonly ListBox _listBox;

    public ProviderSelectionDialog(string title, IList<string> options)
    {
        Title = string.IsNullOrWhiteSpace(title) ? Resources.SelectProviderFallback : title;
        Width = 480;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Thickness(16) };

        var textBlock = new TextBlock
        {
            Text = Title,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(textBlock);

        _listBox = new ListBox { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        _listBox.ItemsSource = options;
        root.Children.Add(_listBox);
        root.Children.Add(new Separator());

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };

        var okButton = new Button { Content = Resources.OK, IsDefault = true };
        okButton.Click += (_, _) =>
        {
            var idx = _listBox.SelectedIndex;
            if (idx < 0) idx = 0;
            Close(idx);
        };
        var cancelButton = new Button { Content = Resources.Cancel, IsCancel = true };
        cancelButton.Click += (_, _) => Close(0);

        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);

        Content = root;
    }
}

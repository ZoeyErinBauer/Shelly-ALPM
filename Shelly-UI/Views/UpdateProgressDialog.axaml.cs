using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Res = Shelly_UI.Assets.Resources;

namespace Shelly_UI.Views;

public partial class UpdateProgressDialog : Window
{
    public bool Success { get; private set; }

    public UpdateProgressDialog()
    {
        InitializeComponent();
    }

    public void AppendOutput(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            OutputText.Text += text + "\n";
            OutputScrollViewer.ScrollToEnd();
        });
    }

    public void SetComplete(bool success)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Success = success;
            CloseButton.IsEnabled = true;
            AppendOutput(success ? $"\n✓ {Res.UpdateCompletedSuccessfully}" : $"\n✗ {Res.UpdateFailedShort}");
        });
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(Success);
    }
}

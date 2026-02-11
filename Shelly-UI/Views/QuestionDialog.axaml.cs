using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Res = Shelly_UI.Assets.Resources;

namespace Shelly_UI.Views;

public partial class QuestionDialog : Window
{
    public bool Result { get; private set; }

    public QuestionDialog()
    {
        InitializeComponent();
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
    }

    public QuestionDialog(string questionText, string? yesButtonText = null, string? noButtonText = null) : this()
    {
        QuestionText.Text = questionText;
        YesButton.Content = yesButtonText ?? Res.Yes;
        NoButton.Content = noButtonText ?? Res.No;
    }

    private void YesButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(true);
    }

    private void NoButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(false);
    }
}

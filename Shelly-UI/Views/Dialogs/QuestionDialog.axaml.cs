using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Shelly_UI.Views;

public partial class QuestionDialog : Window
{
    public bool Result { get; private set; }

    public QuestionDialog()
    {
        InitializeComponent();
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
    }

    public QuestionDialog(string questionText, string yesButtonText = "Yes", string noButtonText = "No") : this()
    {
        QuestionText.Text = questionText;
        YesButton.Content = yesButtonText;
        NoButton.Content = noButtonText;
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

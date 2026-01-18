using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Shelly_UI.Views;

public partial class QuestionDialog : Window
{
    public bool Result { get; private set; }

    public QuestionDialog()
    {
        InitializeComponent();
    }

    public QuestionDialog(string questionText) : this()
    {
        QuestionText.Text = questionText;
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

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Shelly_UI.CustomControls.Dialogs;

public partial class UpdateDialog : Window
{
    public bool Result { get; private set; }

    // ReSharper disable once MemberCanBePrivate.Global
    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(string questionText, string releaseNotes, string yesButtonText = "Yes",
        string noButtonText = "No") : this()
    {
        QuestionText.Text = questionText;
        YesButton.Content = yesButtonText;
        NoButton.Content = noButtonText;
        ReleaseNotes.MarkdownText = releaseNotes; // Use MarkdownText property, not Text
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
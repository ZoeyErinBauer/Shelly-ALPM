using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Shelly_UI.CustomControls.Dialogs;

public partial class PackageBuildDialog : Window
{
    public bool Result { get; private set; }

    // ReSharper disable once MemberCanBePrivate.Global
    public PackageBuildDialog()
    {
        InitializeComponent();
    }

    public PackageBuildDialog(string questionText, string yesButtonText = "Confirm",
        string noButtonText = "Cancel", string packageBuild = "") : this()
    {
        QuestionText.Text = questionText;
        YesButton.Content = yesButtonText;
        NoButton.Content = noButtonText;
        PackageBuildText.Text = packageBuild;
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
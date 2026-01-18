using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Shelly_UI.Views;

namespace Shelly_UI.Tests.Views;

[TestFixture]
public class QuestionDialogTests
{
    [AvaloniaTest]
    public void QuestionDialog_CanBeInstantiated()
    {
        var dialog = new QuestionDialog();
        Assert.That(dialog, Is.Not.Null);
        Assert.That(dialog, Is.InstanceOf<Window>());
    }

    [AvaloniaTest]
    public void QuestionDialog_SetsQuestionText_WhenConstructedWithText()
    {
        const string expectedText = "Do you want to proceed?";
        var dialog = new QuestionDialog(expectedText);
        
        var textBlock = dialog.FindControl<TextBlock>("QuestionText");
        Assert.That(textBlock, Is.Not.Null);
        Assert.That(textBlock!.Text, Is.EqualTo(expectedText));
    }

    [AvaloniaTest]
    public void QuestionDialog_Result_IsFalseByDefault()
    {
        var dialog = new QuestionDialog();
        Assert.That(dialog.Result, Is.False);
    }

    [AvaloniaTest]
    public void QuestionDialog_YesButton_SetsResultToTrue()
    {
        var dialog = new QuestionDialog("Test question");
        
        var yesButton = dialog.FindControl<Button>("YesButton");
        Assert.That(yesButton, Is.Not.Null);
        
        // Simulate button click by raising the Click event
        yesButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        
        Assert.That(dialog.Result, Is.True);
    }

    [AvaloniaTest]
    public void QuestionDialog_NoButton_SetsResultToFalse()
    {
        var dialog = new QuestionDialog("Test question");
        
        var noButton = dialog.FindControl<Button>("NoButton");
        Assert.That(noButton, Is.Not.Null);
        
        // Simulate button click by raising the Click event
        noButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        
        Assert.That(dialog.Result, Is.False);
    }

    [AvaloniaTest]
    public void QuestionDialog_HasYesAndNoButtons()
    {
        var dialog = new QuestionDialog();
        
        var yesButton = dialog.FindControl<Button>("YesButton");
        var noButton = dialog.FindControl<Button>("NoButton");
        
        Assert.That(yesButton, Is.Not.Null);
        Assert.That(noButton, Is.Not.Null);
        Assert.That(yesButton!.Content, Is.EqualTo("Yes"));
        Assert.That(noButton!.Content, Is.EqualTo("No"));
    }

    [AvaloniaTest]
    public void QuestionDialog_Title_IsQuestion()
    {
        var dialog = new QuestionDialog();
        Assert.That(dialog.Title, Is.EqualTo("Question"));
    }
}

using Moq;
using Shelly_UI.ViewModels;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;
using PackageManager.Alpm;
using Microsoft.Reactive.Testing;
using System.Reactive.Linq;
using System.Reactive;

namespace Shelly_UI.Tests.ViewModels;

public class MainWindowViewModelTests : TestScheduler
{
    private Mock<IConfigService> _configServiceMock;
    private Mock<IAppCache> _appCacheMock;
    private Mock<IAlpmManager> _alpmManagerMock;

    [SetUp]
    public void Setup()
    {
        _configServiceMock = new Mock<IConfigService>();
        _appCacheMock = new Mock<IAppCache>();
        _alpmManagerMock = new Mock<IAlpmManager>();
    }

    [Test]
    public void IsProcessing_ShouldBeFalse_Initially()
    {
        var vm = new MainWindowViewModel(_configServiceMock.Object, _appCacheMock.Object, _alpmManagerMock.Object, this);
        Assert.That(vm.IsProcessing, Is.False);
    }

    [Test]
    public void Progress_ShouldUpdate_WhenProgressEventOccurs()
    {
        var vm = new MainWindowViewModel(_configServiceMock.Object, _appCacheMock.Object, _alpmManagerMock.Object, this);

        _alpmManagerMock.Raise(m => m.Progress += null,
            new AlpmProgressEventArgs(AlpmProgressType.AddStart, "test-package", 50, 100, 50));
        
        // Trigger scheduler
        AdvanceBy(1);

        Assert.That(vm.ProgressValue, Is.EqualTo(50));
        Assert.That(vm.ProgressIndeterminate, Is.False);
        Assert.That(vm.ProcessingMessage, Contains.Substring("test-package"));
    }

    [Test]
    public void Progress_ShouldUpdateMessage_WhenPackageNameIsNull()
    {
        var vm = new MainWindowViewModel(_configServiceMock.Object, _appCacheMock.Object, _alpmManagerMock.Object, this);

        _alpmManagerMock.Raise(m => m.Progress += null,
            new AlpmProgressEventArgs(AlpmProgressType.AddStart, null, 75, 100, 75));

        // Trigger scheduler
        AdvanceBy(1);

        Assert.That(vm.ProgressValue, Is.EqualTo(75));
        Assert.That(vm.ProgressIndeterminate, Is.False);
        Assert.That(vm.ProcessingMessage, Contains.Substring("75%"));
    }

    [Test]
    public void Question_ShouldShowPopup_AndSetResponse()
    {
        var vm = new MainWindowViewModel(_configServiceMock.Object, _appCacheMock.Object, _alpmManagerMock.Object, this);
        var args = new AlpmQuestionEventArgs(AlpmQuestionType.InstallIgnorePkg, "Install anyway?");

        // Raise the question event
        _alpmManagerMock.Raise(m => m.Question += null, args);
        AdvanceBy(1);

        // Verify popup is shown
        Assert.That(vm.ShowQuestion, Is.True);
        Assert.That(vm.QuestionText, Is.EqualTo("Install anyway?"));

        // Respond to the question
        vm.RespondToQuestion.Execute("1").Subscribe();
        AdvanceBy(1);

        // Verify popup is hidden and response is set
        Assert.That(vm.ShowQuestion, Is.False);
        Assert.That(args.Response, Is.EqualTo(1));
    }
}

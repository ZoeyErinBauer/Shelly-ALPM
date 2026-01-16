using System;

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Shelly_UI.Assets;
using ReactiveUI;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;
using PackageManager.Alpm;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    private PackageViewModel? _cachedPackages;
    private readonly IServiceProvider _services;
    private IAppCache _appCache;

    public MainWindowViewModel(IConfigService configService, IAppCache appCache, IAlpmManager alpmManager, IServiceProvider services,
        IScheduler? scheduler = null)
    {
        _services = services;
        scheduler ??= RxApp.MainThreadScheduler;
        
        _appCache = appCache;

        var packageOperationEvents = Observable.FromEventPattern<AlpmPackageOperationEventArgs>(
            h => alpmManager.PackageOperation += h,
            h => alpmManager.PackageOperation -= h);

        packageOperationEvents
            .ObserveOn(scheduler)
            .Subscribe(pattern =>
            {
                //Console.WriteLine($@"Got here:" + pattern.EventArgs.EventType);
                var args = pattern.EventArgs;
                switch (args.EventType)
                {
                    case AlpmEventType.PackageOperationStart:
                    case AlpmEventType.TransactionStart:
                    {
                        IsProcessing = true;
                        if (!string.IsNullOrEmpty(args.PackageName))
                        {
                            ProcessingMessage = $"Completing requested actions: {args.PackageName}";
                        }
                        else if (args.EventType == AlpmEventType.TransactionStart)
                        {
                            ProcessingMessage = "Starting transaction...";
                        }
                        else
                        {
                            ProcessingMessage = "Processing...";
                        }

                        ProgressValue = 0;
                        ProgressIndeterminate = true;
                        break;
                    }
                    case AlpmEventType.PackageOperationDone:
                    case AlpmEventType.TransactionDone:
                    {
                        if (args.EventType == AlpmEventType.TransactionDone)
                        {
                            IsProcessing = false;
                            ProcessingMessage = string.Empty;
                            ProgressValue = 0;
                        }

                        break;
                    }
                }
            });

        Observable.FromEventPattern<AlpmProgressEventArgs>(
                h => alpmManager.Progress += h,
                h => alpmManager.Progress -= h)
            .ObserveOn(scheduler)
            .Subscribe(pattern =>
            {
                if (pattern.EventArgs.ProgressType == AlpmProgressType.AddStart ||
                    pattern.EventArgs.ProgressType == AlpmProgressType.RemoveStart)
                {
                    IsProcessing = true;
                }

                if (pattern.EventArgs.Percent >= 100)
                {
                    IsProcessing = false;
                }

                Console.Error.WriteLine($@"Got here:" + pattern.EventArgs.ProgressType);
                var args = pattern.EventArgs;
                if (args.Percent.HasValue)
                {
                    ProgressValue = args.Percent.Value;
                    ProgressIndeterminate = false;
                }

                if (!string.IsNullOrEmpty(args.PackageName))
                {
                    var prefix = args.ProgressType == AlpmProgressType.PackageDownload ? "Downloading" : "Processing";
                    ProcessingMessage = $"{prefix} {args.PackageName}... ({args.Percent}%)";
                }
                else if (args.Percent.HasValue)
                {
                    ProcessingMessage = $"Processing... ({args.Percent}%)";
                }
            });

        packageOperationEvents
            .ObserveOn(scheduler)
            .Where(e => e.EventArgs.EventType != AlpmEventType.TransactionDone)
            .Throttle(TimeSpan.FromSeconds(30), scheduler)
            .Subscribe(_ =>
            {
                Console.Error.WriteLine("Resetting processing state");
                IsProcessing = false;
                ProcessingMessage = string.Empty;
            });

        var questionResponseSubject = new Subject<int>();
        RespondToQuestion = ReactiveCommand.Create<string>(response =>
        {
            if (int.TryParse(response, out var result))
            {
                questionResponseSubject.OnNext(result);
            }
            else
            {
                questionResponseSubject.OnNext(0); // Default to No
            }

            ShowQuestion = false;
        });

        Observable.FromEventPattern<AlpmQuestionEventArgs>(
                h => alpmManager.Question += h,
                h => alpmManager.Question -= h)
            .ObserveOn(scheduler)
            .SelectMany(async pattern =>
            {
                var args = pattern.EventArgs;
                QuestionTitle = GetQuestionTitle(args.QuestionType);
                QuestionText = args.QuestionText;
                ShowQuestion = true;

                // Wait for user response
                var response = await questionResponseSubject.FirstAsync();
                args.Response = response;
                return Unit.Default;
            })
            .Subscribe();

        GoHome = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new HomeViewModel(this, appCache)));
        GoPackages = ReactiveCommand.CreateFromObservable(() =>
        {
            _cachedPackages ??= new PackageViewModel(this, appCache);
            return Router.Navigate.Execute(_cachedPackages);
        });
        GoUpdate = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new UpdateViewModel(this)));
        GoRemove = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new RemoveViewModel(this)));
        GoSetting = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new SettingViewModel(this, configService, _services.GetRequiredService<IUpdateService>(), appCache)));
        
        GoHome.Execute(Unit.Default);
    }
    
    private bool _isPaneOpen = false;

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    private bool _isProcessing;

    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    private int _progressValue;

    public int ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    private bool _progressIndeterminate = true;

    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _progressIndeterminate, value);
    }

    private string _processingMessage = string.Empty;

    public string ProcessingMessage
    {
        get => _processingMessage;
        set => this.RaiseAndSetIfChanged(ref _processingMessage, value);
    }

    private bool _showQuestion;
    public bool ShowQuestion
    {
        get => _showQuestion;
        set => this.RaiseAndSetIfChanged(ref _showQuestion, value);
    }

    private string _questionTitle = string.Empty;
    public string QuestionTitle
    {
        get => _questionTitle;
        set => this.RaiseAndSetIfChanged(ref _questionTitle, value);
    }

    private string _questionText = string.Empty;
    public string QuestionText
    {
        get => _questionText;
        set => this.RaiseAndSetIfChanged(ref _questionText, value);
    }

    public ReactiveCommand<string, Unit> RespondToQuestion { get; }

    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public RoutingState Router { get; } = new RoutingState();

    #region ReactiveCommands

    public static ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoSetting { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoPackages { get; set; } = null!;

    #endregion

    #region MenuItemSelectionNav

    private bool _isPackageOpen;
    
    public bool IsPackageOpen
    {
        get => _isPackageOpen;
        set => this.RaiseAndSetIfChanged(ref _isPackageOpen, value);
    }

    public void TogglePackageMenu()
    {
        if (!IsPaneOpen)
        {
            IsPaneOpen = true;
            IsPackageOpen = true;
        }
        else
        {
            IsPackageOpen = !IsPackageOpen;
        }
    }

    private bool _isAurOpen;
    public bool IsAurOpen 
    {
        get => _isAurOpen;
        set => this.RaiseAndSetIfChanged(ref _isAurOpen, value);
    }
    
    public void ToggleAurMenu()
    {
        if (!IsPaneOpen)
        {
            IsPaneOpen = true;
            IsAurOpen = true;
        }
        else
        {
            IsAurOpen = !IsAurOpen;
        }
    }
    
    private bool _isSnapOpen;
    public bool IsSnapOpen 
    {
        get => _isSnapOpen;
        set => this.RaiseAndSetIfChanged(ref _isSnapOpen, value);
    }
    
    public void ToggleSnapMenu()
    {
        if (!IsPaneOpen)
        {
            IsPaneOpen = true;
            IsSnapOpen = true;
        }
        else
        {
            IsSnapOpen = !IsSnapOpen;
        }
    }
    
    private bool _isFlatpakOpen;
    public bool IsFlatpakOpen 
    {
        get => _isFlatpakOpen;
        set => this.RaiseAndSetIfChanged(ref _isFlatpakOpen, value);
    }
    
    public void ToggleFlatpakMenu()
    {
        if (!IsPaneOpen)
        {
            IsPaneOpen = true;
            IsFlatpakOpen = true;
        }
        else
        {
            IsFlatpakOpen = !IsFlatpakOpen;
        }
    }

    private string GetQuestionTitle(AlpmQuestionType questionType)
    {
        return questionType switch
        {
            AlpmQuestionType.InstallIgnorePkg => "Install Ignore Package?",
            AlpmQuestionType.ReplacePkg => "Replace Package?",
            AlpmQuestionType.ConflictPkg => "Package Conflict",
            AlpmQuestionType.CorruptedPkg => "Corrupted Package",
            AlpmQuestionType.ImportKey => "Import GPG Key?",
            AlpmQuestionType.SelectProvider => "Select Provider",
            _ => "Package Manager Question"
        };
    }

    #endregion
}

public class MenuItemViewModel(
    string label,
    MaterialIconKind icon,
    string toolTip,
    ReactiveCommand<Unit, IRoutableViewModel> command)
{
    public string Label { get; } = label;
    public MaterialIconKind Icon { get; } = icon;

    public string ToolTip { get; } = toolTip;

    public ReactiveCommand<Unit, IRoutableViewModel> Command { get; set; } = command;
}
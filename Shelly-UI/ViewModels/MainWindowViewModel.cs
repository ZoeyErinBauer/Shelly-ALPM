using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using PackageManager.Alpm;
using Shelly_UI.Enums;
using Shelly_UI.Messages;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;
using Shelly_UI.ViewModels.AUR;
using Shelly_UI.ViewModels.Flatpak;

namespace Shelly_UI.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen, IDisposable
{
    private readonly IServiceProvider _services;
    private IAppCache _appCache;
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly ICredentialManager _credentialManager;
    private IConfigService _configService = App.Services.GetRequiredService<IConfigService>();

    private static readonly Regex AlpmProgressPattern =
        new(@"ALPM Progress: (\w+), Pkg: ([^,]+), %: (\d+)", RegexOptions.Compiled);

    private static readonly Regex LogPercentagePattern = new(@"([^:\s\[\]]+): \d+% -> (\d+)%", RegexOptions.Compiled);

    public MainWindowViewModel(IConfigService configService, IAppCache appCache, IAlpmManager alpmManager,
        IServiceProvider services,
        IScheduler? scheduler = null)
    {
        _services = services;
        scheduler ??= RxApp.MainThreadScheduler;

        _appCache = appCache;
        _privilegedOperationService = services.GetRequiredService<IPrivilegedOperationService>();
        _credentialManager = services.GetRequiredService<ICredentialManager>();

        // Subscribe to credential requests
        _credentialManager.CredentialRequested += (sender, args) =>
        {
            // Use the scheduler to ensure we're on the UI thread
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                PasswordPromptReason = args.Reason;
                PasswordInput = string.Empty;
                PasswordErrorMessage = string.Empty;
                ShowPasswordPrompt = true;
                IsGlobalBusy = false;
            });
        };

        // Command to submit password
        SubmitPasswordCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!string.IsNullOrEmpty(PasswordInput))
            {
                _credentialManager.StorePassword(PasswordInput);
                PasswordErrorMessage = "Validating...";

                await _credentialManager.CompleteCredentialRequestAsync(true);

                if (_credentialManager.IsValidated)
                {
                    ShowPasswordPrompt = false;
                    PasswordInput = string.Empty;
                    PasswordErrorMessage = string.Empty;
                }
                else
                {
                    PasswordErrorMessage = "Invalid password. Please try again.";
                }
            }
            else
            {
                PasswordErrorMessage = "Password cannot be empty.";
            }
        });

        // Command to cancel password prompt
        CancelPasswordCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            ShowPasswordPrompt = false;
            PasswordInput = string.Empty;
            PasswordErrorMessage = string.Empty;
            await _credentialManager.CompleteCredentialRequestAsync(false);
        });

        var packageOperationEvents = Observable.FromEventPattern<AlpmPackageOperationEventArgs>(
            h => alpmManager.PackageOperation += h,
            h => alpmManager.PackageOperation -= h);


        packageOperationEvents
            .ObserveOn(scheduler)
            .Subscribe(pattern =>
            {
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

        GoHome = ReactiveCommand.CreateFromObservable(() =>
        {
            ActiveMenu = MenuOptions.None;
            return Router.Navigate.Execute(new HomeViewModel(this, appCache));
        });
        GoPackages = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(new PackageViewModel(this, appCache, _privilegedOperationService,
                _credentialManager)));
        GoUpdate = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(new UpdateViewModel(this, _privilegedOperationService, _credentialManager)));
        GoRemove = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(
                new RemoveViewModel(this, appCache, _privilegedOperationService, _credentialManager)));
        GoSetting = ReactiveCommand.CreateFromObservable(() =>
        {
            IsSettingsOpen = true;
            return SettingRouter.Navigate.Execute(new SettingViewModel(this, configService,
                _services.GetRequiredService<IUpdateService>(), appCache, _privilegedOperationService));
        });
        GoAur = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(new AurViewModel(this, appCache, _privilegedOperationService,
                _credentialManager)));
        GoAurUpdate = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(new AurUpdateViewModel(this, _privilegedOperationService, _credentialManager)));
        GoAurRemove = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(
                new AurRemoveViewModel(this, appCache, _privilegedOperationService, _credentialManager)));
        CloseSettingsCommand = ReactiveCommand.Create(() => IsSettingsOpen = false);

        GoFlatpakRemove = ReactiveCommand.CreateFromObservable(() =>
            Router.Navigate.Execute(new FlatpakRemoveViewModel(this)));

        _navigationMap = new()
        {
            { DefaultViewEnum.HomeScreen, GoHome },
            { DefaultViewEnum.InstallPackage, GoPackages },
            { DefaultViewEnum.RemovePackage, GoRemove },
            { DefaultViewEnum.UpdatePackage, GoUpdate },
            { DefaultViewEnum.UpdateAur, GoAurUpdate },
            { DefaultViewEnum.InstallAur, GoAur },
            { DefaultViewEnum.RemoveAur, GoAurRemove },
            { DefaultViewEnum.InstallFlatpack, GoFlatpak },
            { DefaultViewEnum.RemoveFlatpack, GoFlatpakRemove },
            { DefaultViewEnum.UpdateFlatpack, GoFlatpakUpdate }
        };

        NavigateToDefaultView();

        Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => ConsoleLogService.Instance.Logs.CollectionChanged += h,
                h => ConsoleLogService.Instance.Logs.CollectionChanged -= h)
            .Where(pattern => pattern.EventArgs.Action == NotifyCollectionChangedAction.Add &&
                              pattern.EventArgs.NewItems != null)
            .SelectMany(pattern => pattern.EventArgs.NewItems!.Cast<string>())
            .ObserveOn(scheduler)
            .Subscribe(log =>
            {
                var matchAlpm = AlpmProgressPattern.Match(log);
                var matchFormatted = LogPercentagePattern.Match(log);

                if (matchAlpm.Success)
                {
                    var progressType = matchAlpm.Groups[1].Value;
                    var pkg = matchAlpm.Groups[2].Value.Trim();
                    if (int.TryParse(matchAlpm.Groups[3].Value, out var percent))
                    {
                        var action = progressType switch
                        {
                            "PackageDownload" => "Downloading",
                            "ReinstallStart" => "Reinstalling",
                            "AddStart" => "Installing",
                            "UpgradeStart" => "Updating",
                            "RemoveStart" => "Removing",
                            _ => progressType.Replace("Start", "ing")
                        };

                        GlobalProgressValue = percent;
                        GlobalProgressText = $"{percent}%";
                        GlobalBusyMessage = $"{action} {pkg}...";
                    }
                }
                else if (matchFormatted.Success)
                {
                    var pkg = matchFormatted.Groups[1].Value.Trim();
                    if (int.TryParse(matchFormatted.Groups[2].Value, out var percent))
                    {
                        GlobalProgressValue = percent;
                        GlobalProgressText = $"{percent}%";
                        GlobalBusyMessage = $"Processing {pkg}...";
                    }
                }
            });

        MessageBus.Current.Listen<SettingsChangedMessage>()
            .Subscribe(RefreshUi)
            .DisposeWith(Disposables);
    }

    private void RefreshUi(SettingsChangedMessage msg)
    {
        if (!msg.AurChanged) return;

        IsAurEnabled = !IsAurEnabled;
        if (IsAurOpen)
        {
            IsAurOpen = false;
        }

        this.RaisePropertyChanged(nameof(IsAurEnabled));
    }

    private void NavigateToDefaultView()
    {
        var defaultView = _configService.LoadConfig().DefaultView;

        if (_navigationMap.TryGetValue(defaultView, out var command))
        {
            command.Execute(null);
        }
    }

    private readonly Dictionary<DefaultViewEnum, ICommand> _navigationMap;

    private bool _isGlobalBusy;

    public bool IsGlobalBusy
    {
        get => _isGlobalBusy;
        set => this.RaiseAndSetIfChanged(ref _isGlobalBusy, value);
    }

    private string _globalBusyMessage = "Processing...";

    public string GlobalBusyMessage
    {
        get => _globalBusyMessage;
        set => this.RaiseAndSetIfChanged(ref _globalBusyMessage, value);
    }

    private int _globalProgressValue;

    public int GlobalProgressValue
    {
        get => _globalProgressValue;
        set => this.RaiseAndSetIfChanged(ref _globalProgressValue, value);
    }

    private string _globalProgressText = "0%";

    public string GlobalProgressText
    {
        get => _globalProgressText;
        set => this.RaiseAndSetIfChanged(ref _globalProgressText, value);
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

    #region Password Prompt

    private bool _showPasswordPrompt;

    public bool ShowPasswordPrompt
    {
        get => _showPasswordPrompt;
        set => this.RaiseAndSetIfChanged(ref _showPasswordPrompt, value);
    }

    private string _passwordPromptReason = string.Empty;

    public string PasswordPromptReason
    {
        get => _passwordPromptReason;
        set => this.RaiseAndSetIfChanged(ref _passwordPromptReason, value);
    }

    private string _passwordInput = string.Empty;

    public string PasswordInput
    {
        get => _passwordInput;
        set => this.RaiseAndSetIfChanged(ref _passwordInput, value);
    }

    private string _passwordErrorMessage = string.Empty;

    public string PasswordErrorMessage
    {
        get => _passwordErrorMessage;
        set => this.RaiseAndSetIfChanged(ref _passwordErrorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> SubmitPasswordCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelPasswordCommand { get; }

    #endregion

    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public RoutingState Router { get; } = new RoutingState();

    public RoutingState SettingRouter { get; } = new RoutingState();

    #region ReactiveCommands

    public static ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoSetting { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoPackages { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAur { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAurRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAurUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpakUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpakRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpak { get; set; } = null!;

    public ReactiveCommand<Unit, bool> CloseSettingsCommand { get; set; } = null!;

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

    public bool IsAurEnabled
    {
        get => _configService.LoadConfig().AurEnabled;
        set => _configService.LoadConfig().AurEnabled = value;
    }

    public bool IsFlatpakEnabled
    {
        get => _configService.LoadConfig().FlatPackEnabled;
        set => _configService.LoadConfig().FlatPackEnabled = value;
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

    #region UpdateNotification

    private async Task CheckForUpdates()
    {
        Console.WriteLine("Checking for updates...");
        try
        {
            if (AppContext.BaseDirectory.StartsWith("/usr/share/bin/Shelly") ||
                AppContext.BaseDirectory.StartsWith("/usr/share/Shelly"))
            {
                return;
            }

            var updateAvailable = await _services.GetRequiredService<IUpdateService>().CheckForUpdateAsync();
            if (updateAvailable)
            {
                ShowNotification = true;
                await _appCache.StoreAsync(nameof(CacheEnums.UpdateAvailableCache), true);
                return;
            }

            ShowNotification = false;
            await _appCache.StoreAsync(nameof(CacheEnums.UpdateAvailableCache), false);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to check for updates:");
            Console.Error.WriteLine(e);
        }
    }

    private bool _showNotification = false;

    public bool ShowNotification
    {
        get => _showNotification;
        set => this.RaiseAndSetIfChanged(ref _showNotification, value);
    }

    #endregion

    #region MenuItemsToggle

    private MenuOptions _activeMenu;

    public MenuOptions ActiveMenu
    {
        get => _activeMenu;
        set => this.RaiseAndSetIfChanged(ref _activeMenu, value);
    }

    #endregion

    private bool _isSettingsOpen;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    private readonly CompositeDisposable _disposables = new CompositeDisposable();
    protected CompositeDisposable Disposables => _disposables;

    public void Dispose()
    {
        _disposables?.Dispose();
    }
}
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
using Shelly_UI.Assets;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Messages;
using Shelly_UI.Services;
using Shelly_UI.ViewModels.AUR;
using Shelly_UI.ViewModels.Flatpak;
using Shelly_UI.ViewModels.Packages;

namespace Shelly_UI.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly ICredentialManager _credentialManager;
    private readonly IConfigService _configService = App.Services.GetRequiredService<IConfigService>();
    private QuestionEventArgs? _currentQuestionArgs;

    private static readonly Regex AlpmProgressPattern =
        new(@"ALPM Progress: (\w+), Pkg: ([^,]+), %: (\d+)(?:, bytesRead: (\d+), totalBytes: (\d+))?", RegexOptions.Compiled);

    private static readonly Regex AurProgressPattern =
        new(@"Percent:\s*(\d+)%\s+Message:\s*(.+)", RegexOptions.Compiled);
    
    private static readonly Regex FlatpakProgressPattern =
        new(@"\[DEBUG_LOG\]\s*Progress:\s*(\d+)%\s*-\s*Downloading:\s*([\d.]+)\s*(\w+)/([\d.]+)\s*(\w+)",
            RegexOptions.Compiled);
    
    private static readonly Regex RunningHooksPattern = new(@"(?:\[.*?\]\s*)*Running hooks\.\.\.", RegexOptions.Compiled);

    public MainWindowViewModel(IConfigService configService, IAlpmEventService alpmEventService,
        IServiceProvider services,
        IScheduler? scheduler = null)
    {
        _services = services;
        scheduler ??= RxApp.MainThreadScheduler;
        
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
                PasswordErrorMessage = Resources.Validating;

                await _credentialManager.CompleteCredentialRequestAsync(true);

                if (_credentialManager.IsValidated)
                {
                    ShowPasswordPrompt = false;
                    PasswordInput = string.Empty;
                    PasswordErrorMessage = string.Empty;
                }
                else
                {
                    PasswordErrorMessage = Resources.InvalidPassword;
                }
            }
            else
            {
                PasswordErrorMessage = Resources.EmptyPassword;
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

        var packageOperationEvents = Observable.FromEventPattern<PackageOperationEventArgs>(
            h => alpmEventService.PackageOperation += h,
            h => alpmEventService.PackageOperation -= h);


        packageOperationEvents
            .ObserveOn(scheduler)
            .Subscribe(pattern =>
            {
                var args = pattern.EventArgs;
                switch (args.OperationType)
                {
                    case OperationType.PackageOperationStart:
                    case OperationType.TransactionStart:
                    {
                        IsProcessing = true;
                        if (!string.IsNullOrEmpty(args.PackageName))
                        {
                            ProcessingMessage = string.Format(Resources.CompletingRequestedActions,args.PackageName);
                        }
                        else if (args.OperationType == OperationType.TransactionStart)
                        {
                            ProcessingMessage = Resources.StartTransaction;
                        }
                        else
                        {
                            ProcessingMessage = Resources.Processing;
                        }

                        ProgressValue = 0;
                        ProgressIndeterminate = true;
                        break;
                    }
                    case OperationType.PackageOperationDone:
                    case OperationType.TransactionDone:
                    {
                        if (args.OperationType == OperationType.TransactionDone)
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
            .Where(e => e.EventArgs.OperationType != OperationType.TransactionDone)
            .Throttle(TimeSpan.FromSeconds(30), scheduler)
            .Subscribe(_ =>
            {
                Console.Error.WriteLine("Resetting processing state");
                IsProcessing = false;
                ProcessingMessage = string.Empty;
            });

        RespondToQuestion = ReactiveCommand.Create<string>(response =>
        {
            if (int.TryParse(response, out var result))
            {
                _currentQuestionArgs?.SetResponse(result);
            }
            
            _currentQuestionArgs = null;
            ShowQuestion = false;
        });

        Observable.FromEventPattern<QuestionEventArgs>(
                h => alpmEventService.Question += h,
                h => alpmEventService.Question -= h)
            .ObserveOn(scheduler)
            .Subscribe(pattern =>
            {
                var args = pattern.EventArgs;
                _currentQuestionArgs = args;
                QuestionTitle = GetQuestionTitle(args.QuestionType);
                QuestionText = args.QuestionText;
                
                if (args is { QuestionType: QuestionType.SelectProvider, ProviderOptions.Count: > 0 })
                {
                    IsSelectProviderQuestion = true;
                    ProviderOptions = args.ProviderOptions;
                    SelectedProviderIndex = 0;
                }
                else
                {
                    IsSelectProviderQuestion = false;
                    ProviderOptions = null;
                }
                
                ShowQuestion = true;
            });

        GoHome = ReactiveCommand.CreateFromObservable(() =>
        {
            ActiveMenu = MenuOptions.None;
            var vm = new HomeViewModel(this, _privilegedOperationService);
            return Router.NavigateAndReset.Execute(vm);
        });
        GoPackages = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new PackageViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        GoUpdate = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new UpdateViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        GoManage = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new PackageManagementViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        GoSetting = ReactiveCommand.CreateFromObservable(() =>
        {
            IsSettingsOpen = true;
            var vm = new SettingViewModel(this, configService,
                _services.GetRequiredService<IUpdateService>(), _privilegedOperationService);
            return SettingRouter.NavigateAndReset.Execute(vm);
        });
        GoAur = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new AurViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        GoAurUpdate = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new AurUpdateViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        GoAurRemove = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new AurRemoveViewModel(this, _privilegedOperationService, _credentialManager);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });
        CloseSettingsCommand = ReactiveCommand.Create(() => IsSettingsOpen = false);

        GoFlatpakRemove = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new FlatpakRemoveViewModel(this);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });

        GoFlatpakUpdate = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new FlatpakUpdateViewModel(this);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });

        GoFlatpak = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new FlatpakInstallViewModel(this);
            return Router.NavigateAndReset.Execute(vm).Finally(() => vm?.Dispose());
        });

        GoMetaSearch = ReactiveCommand.CreateFromObservable(() =>
        {
            var vm = new MetaSearchViewModel(this);
            vm.SearchText = MetaSearchString;
            return Router.NavigateAndReset.Execute(vm);
        });

        SearchButtonCommand = ReactiveCommand.Create(() =>
        {
            if (!IsPaneOpen)
            {
                IsPaneOpen = true;
            }
            else
            {
                GoMetaSearch.Execute().Subscribe();
            }
        });


        _navigationMap = new()
        {
            { DefaultViewEnum.HomeScreen, GoHome },
            { DefaultViewEnum.InstallPackage, GoPackages },
            { DefaultViewEnum.PackageManagement, GoManage },
            { DefaultViewEnum.UpdatePackage, GoUpdate },
            { DefaultViewEnum.UpdateAur, GoAurUpdate },
            { DefaultViewEnum.InstallAur, GoAur },
            { DefaultViewEnum.RemoveAur, GoAurRemove },
            { DefaultViewEnum.InstallFlatpack, GoFlatpak },
            { DefaultViewEnum.RemoveFlatpack, GoFlatpakRemove },
            { DefaultViewEnum.UpdateFlatpack, GoFlatpakUpdate }
        };

        NavigateToDefaultView();
        
           Router.CurrentViewModel
          .Select(vm => vm switch
          {
              HomeViewModel => Resources.TitleHome,
              PackageViewModel => Resources.TitleInstallPackages,
              UpdateViewModel => Resources.TitleUpdatePackages,
              PackageManagementViewModel => Resources.TitleManagePackages,
              SettingViewModel => Resources.TitleSettings,
              AurViewModel => Resources.TitleAurInstall,
              AurUpdateViewModel => Resources.TitleAurUpdate,
              AurRemoveViewModel => Resources.TitleAurRemove,
              FlatpakInstallViewModel => Resources.TitleFlatpakInstall,
              FlatpakUpdateViewModel => Resources.TitleFlatpakUpdate,
              FlatpakRemoveViewModel => Resources.TitleFlatpakRemove,
              MetaSearchViewModel => Resources.TitleSearch,
              _ => Resources.AppTitle
          })
          .ObserveOn(scheduler)
          .Subscribe(title => Title = title)
          .DisposeWith(_disposables); 


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
                var matchFlatpak = FlatpakProgressPattern.Match(log);
                var matchHooks = RunningHooksPattern.Match(log);
                var matchAur = AurProgressPattern.Match(log);
                
                if (matchAlpm.Success)
                {
                    var progressType = matchAlpm.Groups[1].Value;
                    var pkg = matchAlpm.Groups[2].Value.Trim();
                    if (int.TryParse(matchAlpm.Groups[3].Value, out var percent))
                    {
                        var action = progressType switch
                         {
                             "PackageDownload" => Resources.ActionDownloading,
                             "ReinstallStart" => Resources.ActionReinstalling,
                             "AddStart" => Resources.ActionInstalling,
                             "UpgradeStart" => Resources.ActionUpdating,
                             "ConflictStart" => Resources.ActionCheckingConflicts,
                             "RemoveStart" => Resources.ActionRemoving,
                             _ => progressType.Replace("Start", "ing")
                         }; 
                       
                        var bytes = matchAlpm.Groups[4].Value;
                        var totalBytes = matchAlpm.Groups[5].Value;

                        if (!string.IsNullOrEmpty(bytes) && !string.IsNullOrEmpty(totalBytes))
                        {
                            GlobalBytesValue = $"{bytes} / {totalBytes} bytes";
                        }
                        
                        GlobalProgressValue = percent;
                        GlobalProgressText = $"{percent}%";
                        GlobalBusyMessage = $"{action} {pkg}...";
                    }
                }
                else if (matchAur.Success)
                {
                    var percent = matchAur.Groups[1].Value;
                    var message = matchAur.Groups[2].Value;
                    GlobalBytesValue = "";
                    GlobalProgressValue = int.Parse(percent);
                    GlobalProgressText = $"{percent}%";
                    GlobalBusyMessage = message;
                }
                else if (matchFlatpak.Success)
                {
                    if (int.TryParse(matchFlatpak.Groups[1].Value, out var percent))
                    {
                        var status = matchFlatpak.Groups[2].Value.Trim();
                        GlobalBytesValue = "";
                        GlobalProgressValue = percent;
                        GlobalProgressText = $"{percent}%";
                        GlobalBusyMessage = "Installing";
                    }
                }
                else if(matchHooks.Success)
                {
                    GlobalBytesValue = "";
                    GlobalBusyMessage = "Running hooks...";
                    GlobalProgressValue = 0;
                    ProgressIndeterminate = true;
                }
            });

        MessageBus.Current.Listen<MainWindowMessage>()
            .Subscribe(RefreshUi)
            .DisposeWith(Disposables);
    }

    private void RefreshUi(MainWindowMessage msg)
    {
        if (msg.MenuLayoutChanged)
        {
            this.RaisePropertyChanged(nameof(UseHorizontalMenu));
            return;
        }

        if (msg.FlatpakEnable)
        {
            IsFlatpakEnabled = !IsFlatpakEnabled;
            if (IsFlatpakOpen)
            {
                IsFlatpakOpen = false;
            }

            this.RaisePropertyChanged(nameof(IsFlatpakEnabled));
            return;
        }

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
    
    private string _globalBytesValue;

    public string GlobalBytesValue
    {
        get => _globalBytesValue;
        set => this.RaiseAndSetIfChanged(ref _globalBytesValue, value);
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

    private List<string>? _providerOptions;

    public List<string>? ProviderOptions
    {
        get => _providerOptions;
        set => this.RaiseAndSetIfChanged(ref _providerOptions, value);
    }

    private bool _isSelectProviderQuestion;

    public bool IsSelectProviderQuestion
    {
        get => _isSelectProviderQuestion;
        set => this.RaiseAndSetIfChanged(ref _isSelectProviderQuestion, value);
    }

    private int _selectedProviderIndex;

    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedProviderIndex, value);
    }


    public string SuccessColor
    {
        get => _configService.LoadConfig().AccentColor ?? "#2E7D32";
        set => _configService.LoadConfig().AccentColor = value;
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

    private IRoutableViewModel? _currentViewModel;

    public RoutingState Router { get; } = new RoutingState();

    public RoutingState SettingRouter { get; } = new RoutingState();

    private string _title = "Shelly";

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    #region ReactiveCommands

    public static ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoManage { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoSetting { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoPackages { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAur { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAurRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoAurUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpakUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpakRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoFlatpak { get; set; } = null!;

    public ReactiveCommand<Unit, IRoutableViewModel> GoMetaSearch { get; set; } = null!;

    public ReactiveCommand<Unit, Unit> SearchButtonCommand { get; set; } = null!;

    public ReactiveCommand<Unit, bool> CloseSettingsCommand { get; set; } = null!;

    #endregion

    #region MenuItemSelectionNav

    private string _metaSearchString;

    public string MetaSearchString
    {
        get => _metaSearchString;
        set => this.RaiseAndSetIfChanged(ref _metaSearchString, value);
    }

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

    public bool UseHorizontalMenu
    {
        get => _configService.LoadConfig().UseHorizontalMenu;
        set => _configService.LoadConfig().UseHorizontalMenu = value;
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

    private string GetQuestionTitle(QuestionType questionType)
    {
        return questionType switch
        {
            QuestionType.InstallIgnorePkg => Resources.QuestionInstallIgnorePkg,
            QuestionType.ReplacePkg => Resources.QuestionReplacePkg,
            QuestionType.ConflictPkg => Resources.QuestionConflictPkg,
            QuestionType.CorruptedPkg => Resources.QuestionCorruptedPkg,
            QuestionType.ImportKey => Resources.QuestionImportKey,
            QuestionType.SelectProvider => Resources.QuestionSelectProvider,
            _ => Resources.QuestionDefault
        };
    }

    #endregion

    #region ActionToast

    private bool _showActionToast;

    public bool ShowActionToast
    {
        get => _showActionToast;
        set => this.RaiseAndSetIfChanged(ref _showActionToast, value);
    }

    private string _actionToastMessage = string.Empty;

    public string ActionToastMessage
    {
        get => _actionToastMessage;
        set => this.RaiseAndSetIfChanged(ref _actionToastMessage, value);
    }

    private bool _actionToastIsSuccess = true;

    public bool ActionToastIsSuccess
    {
        get => _actionToastIsSuccess;
        set => this.RaiseAndSetIfChanged(ref _actionToastIsSuccess, value);
    }

    private IDisposable? _toastDismissTimer;

    public void ShowToast(string message, bool isSuccess = true, int durationMs = 4000)
    {
        _toastDismissTimer?.Dispose();

        ActionToastMessage = message;
        ActionToastIsSuccess = isSuccess;
        ShowActionToast = true;

        _toastDismissTimer = Observable.Timer(TimeSpan.FromMilliseconds(durationMs))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ShowActionToast = false);
    }

    public void DismissToast()
    {
        _toastDismissTimer?.Dispose();
        ShowActionToast = false;
    }

    public ICommand DismissToastCommand => ReactiveCommand.Create(DismissToast);

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

    private void DisposeCurrentViewModel()
    {
        if (_currentViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _currentViewModel = null;
    }

    public void Dispose()
    {
        DisposeCurrentViewModel();
        _disposables?.Dispose();
    }
}
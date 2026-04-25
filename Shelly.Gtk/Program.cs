using System.Runtime;
using System.Reflection;
using Gtk;
using Microsoft.Extensions.DependencyInjection;
using Shelly.Gtk.Services;
using Shelly.Gtk.Services.TrayServices;
using Shelly.Gtk.Windows;
using Shelly.Gtk.Windows.AUR;
using Shelly.Gtk.Windows.Dialog;
using Shelly.Gtk.Windows.Flatpak;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Windows.Packages;
using Settings = Shelly.Gtk.Windows.Settings;


namespace Shelly.Gtk;

sealed class Program
{
    private static string? _requestedPage;

    public static int Main(string[] args)
    {
        //GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Parse --page argument
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--page" && i + 1 < args.Length)
            {
                _requestedPage = args[i + 1];
                break;
            }
        }

        ServiceCollection serviceCollection = new();
        var serviceProvider = ServiceBuilder.CreateDependencyInjection(serviceCollection);

        var application = Application.New(ShellyConstants.Service,
            Gio.ApplicationFlags.DefaultFlags | Gio.ApplicationFlags.HandlesCommandLine);

        application.OnCommandLine += (sender, e) =>
        {
            application.Activate();
            return 0;
        };


        application.OnActivate += (sender, _) =>
        {
            if (serviceProvider!.GetService<IConfigService>()!.LoadConfig().TrayEnabled)
                TrayStartService.Start();

            var existingWindow = application.GetActiveWindow();
            if (existingWindow != null)
            {
                existingWindow.Present();
                return;
            }

            var cssProvider = CssProvider.New();
            cssProvider.LoadFromString(ResourceHelper.LoadAsset("Assets/style.css"));
            StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, cssProvider, 800);

            var iconTheme = IconTheme.GetForDisplay(Gdk.Display.GetDefault()!);
            iconTheme.AddSearchPath("Assets/svg");

            var mainBuilder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/MainWindow.ui"), -1);
            var window = (ApplicationWindow)mainBuilder.GetObject("MainWindow")!;

            window.SetIconName("shelly");
            window.Application = application;

            var menuBuilder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/MainMenu.ui"), -1);
            var appMenu = (Gio.Menu)menuBuilder.GetObject("AppMenu")!;
            application.Menubar = appMenu;

            var quitAction = Gio.SimpleAction.New("quit", null);
            quitAction.OnActivate += (_, _) => application.Quit();
            application.AddAction(quitAction);

            var preferencesAction = Gio.SimpleAction.New("preferences", null);
            preferencesAction.OnActivate += (_, _) => Console.WriteLine("Preferences clicked");
            application.AddAction(preferencesAction);

            var aboutAction = Gio.SimpleAction.New("about", null);
            aboutAction.OnActivate += (_, _) => Console.WriteLine("About clicked");
            application.AddAction(aboutAction);

            var contentArea = (Box)mainBuilder.GetObject("ContentArea")!;
            var mainBox = (Box)mainBuilder.GetObject("MainBox")!;
            var sidebarBox = (Box)mainBuilder.GetObject("SidebarBox")!;
            var collapseButton = (Button)mainBuilder.GetObject("CollapseButton")!;
            
            var sidebarLabels = new List<Widget>
            {
                (Widget)mainBuilder.GetObject("CollapseLabel")!,
                (Widget)mainBuilder.GetObject("HomeLabel")!,
                (Widget)mainBuilder.GetObject("PackagesHeader")!,
                (Widget)mainBuilder.GetObject("InstallPackagesLabel")!,
                (Widget)mainBuilder.GetObject("UpdatePackagesLabel")!,
                (Widget)mainBuilder.GetObject("ManagePackagesLabel")!,
                (Widget)mainBuilder.GetObject("AurHeader")!,
                (Widget)mainBuilder.GetObject("InstallAurLabel")!,
                (Widget)mainBuilder.GetObject("UpdateAurLabel")!,
                (Widget)mainBuilder.GetObject("RemoveAurLabel")!,
                (Widget)mainBuilder.GetObject("FlatpakHeader")!,
                (Widget)mainBuilder.GetObject("InstallFlatpakLabel")!,
                (Widget)mainBuilder.GetObject("UpdateFlatpakLabel")!,
                (Widget)mainBuilder.GetObject("RemoveFlatpakLabel")!,
                (Widget)mainBuilder.GetObject("AppImageHeader")!,
                (Widget)mainBuilder.GetObject("ManageAppImageLabel")!,
                (Widget)mainBuilder.GetObject("SettingsLabel")!
            };

            var isSidebarCollapsed = false;
            collapseButton.OnClicked += (_, _) =>
            {
                isSidebarCollapsed = !isSidebarCollapsed;
                foreach (var label in sidebarLabels)
                {
                    label.Visible = !isSidebarCollapsed;
                }
                sidebarBox.WidthRequest = isSidebarCollapsed ? 50 : 180;
                collapseButton.TooltipText = isSidebarCollapsed ? "Expand" : "Collapse";
                
                foreach (var buttonId in new[] { 
                    "CollapseButton", "HomeButton", "InstallPackagesButton", "UpdatePackagesButton", "ManagePackagesButton",
                    "InstallAurButton", "UpdateAurButton", "RemoveAurButton",
                    "InstallFlatpakButton", "UpdateFlatpakButton", "RemoveFlatpakButton",
                    "ManageAppImageButton",
                    "SettingsButton" 
                })
                {
                    var button = (Button)mainBuilder.GetObject(buttonId)!;
                    if (button.Child is Box buttonBox)
                    {
                        buttonBox.Halign = isSidebarCollapsed ? Align.Center : Align.Start;
                    }
                }
            };

            var homeButton = (Button)mainBuilder.GetObject("HomeButton")!;
            var settingsButton = (Button)mainBuilder.GetObject("SettingsButton")!;
            
            var installPackagesButton = (Button)mainBuilder.GetObject("InstallPackagesButton")!;
            var updatePackagesButton = (Button)mainBuilder.GetObject("UpdatePackagesButton")!;
            var managePackagesButton = (Button)mainBuilder.GetObject("ManagePackagesButton")!;
            
            var aurBox = (Box)mainBuilder.GetObject("AurBox")!;
            var installAurButton = (Button)mainBuilder.GetObject("InstallAurButton")!;
            var updateAurButton = (Button)mainBuilder.GetObject("UpdateAurButton")!;
            var removeAurButton = (Button)mainBuilder.GetObject("RemoveAurButton")!;
            
            var flatpakBox = (Box)mainBuilder.GetObject("FlatpakBox")!;
            var installFlatpakButton = (Button)mainBuilder.GetObject("InstallFlatpakButton")!;
            var updateFlatpakButton = (Button)mainBuilder.GetObject("UpdateFlatpakButton")!;
            var removeFlatpakButton = (Button)mainBuilder.GetObject("RemoveFlatpakButton")!;
            
            var appImageBox = (Box)mainBuilder.GetObject("AppImageBox")!;
            var manageAppImageButton = (Button)mainBuilder.GetObject("ManageAppImageButton")!;

            var configService = serviceProvider.GetRequiredService<IConfigService>();
            var initialConfig = configService.LoadConfig();

            aurBox.Visible = initialConfig.AurEnabled;
            flatpakBox.Visible = initialConfig.FlatPackEnabled;
            appImageBox.Visible = initialConfig.AppImageEnabled;

            //Setting window height
            window.DefaultHeight = double.ConvertToInteger<int>(initialConfig.WindowHeight);
            window.DefaultWidth = double.ConvertToInteger<int>(initialConfig.WindowWidth);
            uint resizeTimerId = 0;

            window.OnNotify += (_, args) =>
            {
                if (args.Pspec.GetName() is not ("default-width" or "default-height")) return;
                if (resizeTimerId != 0)
                    GLib.Functions.SourceRemove(resizeTimerId);

                resizeTimerId = GLib.Functions.TimeoutAdd(0, 500, () =>
                {
                    var config = configService.LoadConfig();
                    config.WindowWidth = window.DefaultWidth;
                    config.WindowHeight = window.DefaultHeight;
                    configService.SaveConfig(config);
                    resizeTimerId = 0;
                    return false;
                });
            };

            var horizontalActionBar = (ActionBar)mainBuilder.GetObject("HorizontalActionBar")!;
            
            configService.ConfigSaved += (_, updatedConfig) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    aurBox.Visible = updatedConfig.AurEnabled;
                    flatpakBox.Visible = updatedConfig.FlatPackEnabled;
                    appImageBox.Visible = updatedConfig.AppImageEnabled;

                    horizontalActionBar.Visible = updatedConfig.UseOldMenu;
                    sidebarBox.Visible = !updatedConfig.UseOldMenu;

                    if (mainBuilder.GetObject("AurMenuButton") is MenuButton aurMenuButton)
                        aurMenuButton.Visible = updatedConfig.AurEnabled;
                    if (mainBuilder.GetObject("FlatpakMenuButton") is MenuButton flatpakMenuButton)
                        flatpakMenuButton.Visible = updatedConfig.FlatPackEnabled;
                    if (mainBuilder.GetObject("AppImageMenuButton") is MenuButton appImageMenuButton)
                        appImageMenuButton.Visible = updatedConfig.AppImageEnabled;

                    return false;
                });
            };

            var sidebarButtons = new Dictionary<string, Button>
            {
                { "HomeButton", homeButton },
                { "SettingsButton", settingsButton },
                { "InstallPackagesButton", installPackagesButton },
                { "UpdatePackagesButton", updatePackagesButton },
                { "ManagePackagesButton", managePackagesButton },
                { "InstallAurButton", installAurButton },
                { "UpdateAurButton", updateAurButton },
                { "RemoveAurButton", removeAurButton },
                { "InstallFlatpakButton", installFlatpakButton },
                { "UpdateFlatpakButton", updateFlatpakButton },
                { "RemoveFlatpakButton", removeFlatpakButton },
                { "ManageAppImageButton", manageAppImageButton }
            };

            IShellyWindow? currentPage = null;

            homeButton.OnClicked += (_, _) => NavigateTo<HomeWindow>("HomeButton");
            settingsButton.OnClicked += (_, _) => NavigateTo<Settings>("SettingsButton");
            
            installPackagesButton.OnClicked += (_, _) => NavigateTo<PackageInstall>("InstallPackagesButton");
            updatePackagesButton.OnClicked += (_, _) => NavigateTo<PackageUpdate>("UpdatePackagesButton");
            managePackagesButton.OnClicked += (_, _) => NavigateTo<PackageManagement>("ManagePackagesButton");
            
            installAurButton.OnClicked += (_, _) => NavigateTo<AurInstall>("InstallAurButton");
            updateAurButton.OnClicked += (_, _) => NavigateTo<AurUpdate>("UpdateAurButton");
            removeAurButton.OnClicked += (_, _) => NavigateTo<AurRemove>("RemoveAurButton");
            
            installFlatpakButton.OnClicked += (_, _) => NavigateTo<FlatpakInstall>("InstallFlatpakButton");
            updateFlatpakButton.OnClicked += (_, _) => NavigateTo<FlatpakUpdate>("UpdateFlatpakButton");
            removeFlatpakButton.OnClicked += (_, _) => NavigateTo<FlatpakRemove>("RemoveFlatpakButton");
            
            manageAppImageButton.OnClicked += (_, _) => NavigateTo<AppImage>("ManageAppImageButton");

            horizontalActionBar.Visible = initialConfig.UseOldMenu;
            sidebarBox.Visible = !initialConfig.UseOldMenu;

            // Always wire horizontal menu events regardless of initial visibility
            var homeButtonHoriz = (Button)mainBuilder.GetObject("HomeButtonHorizontal")!;
            var settingsButtonHoriz = (Button)mainBuilder.GetObject("SettingsButtonHorizontal")!;
            var aurMenuButton = (MenuButton)mainBuilder.GetObject("AurMenuButton")!;
            var flatpakMenuButton = (MenuButton)mainBuilder.GetObject("FlatpakMenuButton")!;
            var appImageMenuButton = (MenuButton)mainBuilder.GetObject("AppImageMenuButton")!;

            aurMenuButton.Visible = initialConfig.AurEnabled;
            flatpakMenuButton.Visible = initialConfig.FlatPackEnabled;
            appImageMenuButton.Visible = initialConfig.AppImageEnabled;

            homeButtonHoriz.OnClicked += (_, _) => NavigateTo<HomeWindow>("HomeButton");
            settingsButtonHoriz.OnClicked += (_, _) => NavigateTo<Settings>("SettingsButton");

            AddAction("install-packages", () => NavigateTo<PackageInstall>("InstallPackagesButton"));
            AddAction("update-packages", () => NavigateTo<PackageUpdate>("UpdatePackagesButton"));
            AddAction("manage-packages", () => NavigateTo<PackageManagement>("ManagePackagesButton"));
            AddAction("install-aur", () => NavigateTo<AurInstall>("InstallAurButton"));
            AddAction("update-aur", () => NavigateTo<AurUpdate>("UpdateAurButton"));
            AddAction("remove-aur", () => NavigateTo<AurRemove>("RemoveAurButton"));
            AddAction("install-flatpak", () => NavigateTo<FlatpakInstall>("InstallFlatpakButton"));
            AddAction("update-flatpak", () => NavigateTo<FlatpakUpdate>("UpdateFlatpakButton"));
            AddAction("remove-flatpak", () => NavigateTo<FlatpakRemove>("RemoveFlatpakButton"));
            AddAction("manage-appimage", () => NavigateTo<AppImage>("ManageAppImageButton"));

            var mainOverlay = (Overlay)mainBuilder.GetObject("MainOverlay")!;

            if (!initialConfig.NewInstallInitSettings)
            {
                sidebarBox.Visible = false;
                horizontalActionBar.Visible = false;

                var setupWindow = serviceProvider.GetRequiredService<SetupWindow>();
                var setupWidget = setupWindow.CreateWindow();
                
                contentArea.Append(setupWidget);
                currentPage = setupWindow;

                setupWindow.SetupFinished += (_, _) =>
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        var updatedConfig = configService.LoadConfig();
                        sidebarBox.Visible = !updatedConfig.UseOldMenu;
                        horizontalActionBar.Visible = updatedConfig.UseOldMenu;

                        contentArea.Remove(setupWidget);
                        setupWindow.Dispose();

                        var homeWindow = serviceProvider.GetRequiredService<HomeWindow>();
                        contentArea.Append(homeWindow.CreateWindow());
                        currentPage = homeWindow;
                        UpdateSelectedButton("HomeButton");
                        return false;
                    });
                };
            }
            else
            {
                var initialHomeWindow = serviceProvider.GetRequiredService<HomeWindow>();
                contentArea.Append(initialHomeWindow.CreateWindow());
                currentPage = initialHomeWindow;
                UpdateSelectedButton("HomeButton");
            }

            // Navigate to requested page from CLI args (ignored during first-launch setup)
            if (_requestedPage != null && currentPage is not SetupWindow)
            {
                switch (_requestedPage)
                {
                    case "flatpak-install":
                        NavigateTo<FlatpakInstall>("InstallFlatpakButton");
                        break;
                    case "flatpak-update":
                        NavigateTo<FlatpakUpdate>("UpdateFlatpakButton");
                        break;
                    case "flatpak-remove":
                        NavigateTo<FlatpakRemove>("RemoveFlatpakButton");
                        break;
                    case "aur-install":
                        NavigateTo<AurInstall>("InstallAurButton");
                        break;
                    case "aur-update":
                        NavigateTo<AurUpdate>("UpdateAurButton");
                        break;
                    case "aur-remove":
                        NavigateTo<AurRemove>("RemoveAurButton");
                        break;
                    case "install-packages":
                        NavigateTo<PackageInstall>("InstallPackagesButton");
                        break;
                    case "update-packages":
                        NavigateTo<PackageUpdate>("UpdatePackagesButton");
                        break;
                    case "manage-packages":
                        NavigateTo<PackageManagement>("ManagePackagesButton");
                        break;
                }
            }

            var lockoutDialog = serviceProvider.GetRequiredService<LockoutDialog>();

            var keyController = EventControllerKey.New();
            keyController.OnKeyPressed += (_, args) =>
            {
                if (!ShouldHandleNavigationShortcut())
                {
                    return false;
                }

                return TryHandleNavigationShortcut(args.Keyval, args.State);
            };
            window.AddController(keyController);

            //Subscribing to credential required to trigger the password dialog
            var credentialManager = serviceProvider.GetRequiredService<ICredentialManager>();
            credentialManager.CredentialRequested += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    var dialog = serviceProvider.GetRequiredService<PasswordDialog>();
                    dialog.ShowPasswordDialog(mainOverlay, e.Reason);
                    return false;
                });
            };

            var alpmEventService = serviceProvider.GetRequiredService<IAlpmEventService>();
            
            alpmEventService.Question += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    var dialog = serviceProvider.GetRequiredService<AlpmEventDialog>();
                    AlpmEventDialog.ShowAlpmEventDialog(mainOverlay, e);
                    return false;
                });
            };

            var genericQuestionService = serviceProvider.GetRequiredService<IGenericQuestionService>();
            genericQuestionService.Question += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    GenericQuestionDialog.ShowGenericQuestionDialog(mainOverlay, e);
                    return false;
                });
            };

            genericQuestionService.PackageBuildRequested += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    PackageBuildDialog.ShowPackageBuildDialog(mainOverlay, e);
                    return false;
                });
            };
            
            genericQuestionService.ToastMessageRequested += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    ToastMessageDialog.ShowToastMessage(mainOverlay, e);
                    return false;
                });
            };
            
            genericQuestionService.Dialog += (s, e) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    GenericOverlay.ShowGenericOverlay(mainOverlay, e.Box, e);
                    return false;
                });
            };


            window.Show();

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            if (assemblyVersion != configService.LoadConfig().CurrentVersion)
            {
                if (!configService.LoadConfig().NewInstall)
                {
                    var notes = new GitHubUpdateService().PullReleaseNotesAsync();
                    ReleaseNotesDialog.ShowReleaseNotesDialog(mainOverlay, notes.Result);
                    
                    var config = configService.LoadConfig();
                    config.CurrentVersion = assemblyVersion;
                    configService.SaveConfig(config);
                }
                else
                {
                    var config = configService.LoadConfig();
                    config.NewInstall = false;
                    config.CurrentVersion = assemblyVersion;
                    configService.SaveConfig(config);
                }
            }

            var lockoutService = serviceProvider.GetRequiredService<ILockoutService>();

            lockoutService.StatusChanged += (_, lockoutArgs) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    if (lockoutArgs.IsLocked)
                    {
                        if (!lockoutDialog.IsVisible)
                        {
                            lockoutDialog.Show(mainOverlay, lockoutArgs.Description ?? "Processing...",
                                lockoutArgs.Progress, lockoutArgs.IsIndeterminate);
                        }
                        else
                        {
                            lockoutDialog.UpdateStatus(lockoutArgs.Description ?? "Processing...",
                                lockoutArgs.Progress, lockoutArgs.IsIndeterminate);
                        }
                    }
                    else
                    {
                        lockoutDialog.ShowCloseButton();
                    }
                    return false;
                });
            };

            lockoutService.LogLineReceived += (_, logLine) =>
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    lockoutDialog.AppendLogLine(logLine);
                    return false;
                });
            };

            return;

            void UpdateSelectedButton(string selectedId)
            {
                foreach (var kvp in sidebarButtons)
                {
                    if (kvp.Key == selectedId)
                    {
                        kvp.Value.AddCssClass("selected");
                    }
                    else
                    {
                        kvp.Value.RemoveCssClass("selected");
                    }
                }
            }

            void NavigateTo<T>(string? buttonId = null) where T : IShellyWindow
            {
                NavigateWithQuery<T>(null, buttonId);
            }

            void NavigateWithQuery<T>(string? query, string? buttonId = null) where T : IShellyWindow
            {
                if (currentPage is SetupWindow)
                {
                    return;
                }

                if (buttonId != null)
                {
                    UpdateSelectedButton(buttonId);
                }

                while (contentArea.GetFirstChild() is { } child)
                {
                    contentArea.Remove(child);
                    child.Unparent();
                }
                

                currentPage?.Dispose();
                currentPage = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var page = serviceProvider.GetRequiredService<T>();
                if (page is Settings settings)
                {
                    settings.NavigationToHomeRequested += () => NavigateTo<HomeWindow>("HomeButton");
                }

                if (page is MetaSearch metaSearch && query != null)
                {
                    contentArea.Append(metaSearch.CreateWindow(query));
                }
                else
                {
                    contentArea.Append(page.CreateWindow());
                }

                currentPage = page;
            }

            void AddAction(string name, Action onActivate)
            {
                var action = Gio.SimpleAction.New(name, null);
                action.OnActivate += (_, _) => { onActivate(); };
                application.AddAction(action);
            }

            bool ShouldHandleNavigationShortcut()
            {
                if (lockoutDialog.IsVisible)
                {
                    return false;
                }

                if (HasBlockingOverlay())
                {
                    return false;
                }

                var focus = window.GetFocus();
                if (focus == null)
                {
                    return true;
                }

                if (!IsDescendantOf(focus, mainBox))
                {
                    return false;
                }

                return !IsEditableWidget(focus);
            }

            bool HasBlockingOverlay()
            {
                for (Widget? child = mainOverlay.GetFirstChild(); child != null; child = child.GetNextSibling())
                {
                    if (child == mainBox || !child.Visible)
                    {
                        continue;
                    }

                    if (child.HasCssClass("toast-message"))
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }

            bool TryHandleNavigationShortcut(uint keyval, Gdk.ModifierType state)
            {
                var relevantModifiers = state & (Gdk.ModifierType.ControlMask | Gdk.ModifierType.AltMask |
                                                Gdk.ModifierType.ShiftMask | Gdk.ModifierType.SuperMask |
                                                Gdk.ModifierType.MetaMask);
                if (relevantModifiers != (Gdk.ModifierType.ControlMask | Gdk.ModifierType.AltMask))
                {
                    return false;
                }

                var key = char.ToLowerInvariant((char)keyval);
                switch (key)
                {
                    case 'i':
                        NavigateTo<PackageInstall>();
                        return true;
                    case 'u':
                        NavigateTo<PackageUpdate>();
                        return true;
                    case 'm':
                        NavigateTo<PackageManagement>();
                        return true;
                    default:
                        return false;
                }
            }

            static bool IsEditableWidget(Widget? widget)
            {
                while (widget != null)
                {
                    if (widget is Entry or SearchEntry or PasswordEntry or TextView or SpinButton)
                    {
                        return true;
                    }

                    widget = widget.GetParent();
                }

                return false;
            }

            static bool IsDescendantOf(Widget widget, Widget ancestor)
            {
                Widget? current = widget;
                while (current != null)
                {
                    if (current == ancestor)
                    {
                        return true;
                    }

                    current = current.GetParent();
                }

                return false;
            }
        };

        return application.Run(args);
    }
}

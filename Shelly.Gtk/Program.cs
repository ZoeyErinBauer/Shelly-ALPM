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
using Shelly.Gtk.UiModels;
using Shelly.Gtk.Windows.Packages;
using Settings = Shelly.Gtk.Windows.Settings;


namespace Shelly.Gtk;

sealed class Program
{
    private static string? _requestedPage;

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern int getuid();

    private static void EnsureSessionEnvironment()
    {
        var uid = getuid();

        // 1. XDG_RUNTIME_DIR — required for the session bus socket path
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")))
        {
            var runtime = $"/run/user/{uid}";
            if (Directory.Exists(runtime))
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtime);
        }

        // 2. DBUS_SESSION_BUS_ADDRESS — dconf needs this to read GSettings
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            var rd = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(rd))
            {
                var sock = $"{rd}/bus";
                if (File.Exists(sock))
                    Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", $"unix:path={sock}");
            }
        }

        // 3. XDG_DATA_DIRS — needed so GIO finds compiled GSettings schemas + themes
        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrEmpty(dataDirs) || !dataDirs.Contains("/usr/share"))
        {
            Environment.SetEnvironmentVariable(
                "XDG_DATA_DIRS",
                "/usr/local/share:/usr/share" + (string.IsNullOrEmpty(dataDirs) ? "" : ":" + dataDirs));
        }

        // 4. XDG_CURRENT_DESKTOP — some theme bits key off this
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")))
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", "KDE");

        // 5. Make GIO use dconf instead of falling back to memory backend
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GSETTINGS_BACKEND")))
            Environment.SetEnvironmentVariable("GSETTINGS_BACKEND", "dconf");
    }

    private static void ApplyKdeGtkTheme()
    {
        // If the user already forced GTK_THEME, respect it.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GTK_THEME")))
            return;

        var home = Environment.GetEnvironmentVariable("HOME") ?? "/root";
        string? themeName = null;
        bool preferDark = false;

        // 1. Preferred source: ~/.config/gtk-4.0/settings.ini (written by kde-gtk-config)
        var gtk4Ini = Path.Combine(home, ".config", "gtk-4.0", "settings.ini");
        if (File.Exists(gtk4Ini))
        {
            foreach (var raw in File.ReadAllLines(gtk4Ini))
            {
                var line = raw.Trim();
                if (line.StartsWith("gtk-theme-name", StringComparison.Ordinal))
                    themeName = ValueAfterEquals(line);
                else if (line.StartsWith("gtk-application-prefer-dark-theme", StringComparison.Ordinal))
                {
                    var v = ValueAfterEquals(line);
                    preferDark = v is "1" or "true" or "True";
                }
            }
        }

        // 2. Fallback: detect dark from kdeglobals ColorScheme
        if (!preferDark)
        {
            var kdeGlobals = Path.Combine(home, ".config", "kdeglobals");
            if (File.Exists(kdeGlobals))
            {
                foreach (var raw in File.ReadAllLines(kdeGlobals))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("ColorScheme=", StringComparison.Ordinal))
                    {
                        var scheme = line["ColorScheme=".Length..];
                        if (scheme.Contains("Dark", StringComparison.OrdinalIgnoreCase))
                            preferDark = true;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(themeName))
            themeName = "Adwaita";

        var full = preferDark ? $"{themeName}:dark" : themeName;
        Environment.SetEnvironmentVariable("GTK_THEME", full);
        Environment.SetEnvironmentVariable("GTK_APPLICATION_PREFER_DARK_THEME", preferDark ? "1" : "0");
    }

    private static string? ValueAfterEquals(string line)
    {
        var i = line.IndexOf('=');
        return i < 0 ? null : line[(i + 1)..].Trim().Trim('"', '\'');
    }

    public static int Main(string[] args)
    {
        EnsureSessionEnvironment();
        ApplyKdeGtkTheme();
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
            StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, cssProvider, 600);

            var iconTheme = IconTheme.GetForDisplay(Gdk.Display.GetDefault()!);
            iconTheme.AddSearchPath("Assets/svg");

            var mainBuilder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/MainWindow.ui"), -1);
            var window = (ApplicationWindow)mainBuilder.GetObject("MainWindow")!;

            window.SetIconName("shelly");
            window.Application = application;

            var menuBuilder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/MainMenu.ui"), -1);
            var appMenu = (Gio.Menu)menuBuilder.GetObject("AppMenu")!;
            application.Menubar = appMenu;

            var mainBox = (Box)mainBuilder.GetObject("MainBox")!;
            var settingsStack = (Stack)mainBuilder.GetObject("settings_stack")!;
            var packagesPageBox = (Box)mainBuilder.GetObject("packages_page_box")!;
            var aurPageBox = (Box)mainBuilder.GetObject("aur_page_box")!;
            var flatpakPageBox = (Box)mainBuilder.GetObject("flatpak_page_box")!;
            var appImagePageBox = (Box)mainBuilder.GetObject("appimage_page_box")!;
            var settingsPageBox = (Box)mainBuilder.GetObject("settings_page_box")!;

            var mainOverlay = (Overlay)mainBuilder.GetObject("MainOverlay")!;

            var quitAction = Gio.SimpleAction.New("quit", null);
            quitAction.OnActivate += (_, _) => application.Quit();
            application.AddAction(quitAction);

            var preferencesAction = Gio.SimpleAction.New("preferences", null);
            preferencesAction.OnActivate += (_, _) => settingsStack.SetVisibleChildName("settings_page");
            application.AddAction(preferencesAction);

            var archNews = Gio.SimpleAction.New("news", null);
            archNews.OnActivate += (_, _) =>
            {
                new ArchNewsDialog(serviceProvider.GetRequiredService<IArchNewsService>(), mainOverlay)
                    .OpenArchNewsOverlay();
            };
            application.AddAction(archNews);

            var cacheCleaner = Gio.SimpleAction.New("cacheclean", null);
            cacheCleaner.OnActivate += (_, _) =>
            {
                new CacheCleanerDialog(serviceProvider.GetRequiredService<IGenericQuestionService>(),
                    serviceProvider.GetRequiredService<IPrivilegedOperationService>(),
                    serviceProvider.GetRequiredService<ILockoutService>(), mainOverlay).OpenCacheCleanDialog();
            };
            application.AddAction(cacheCleaner);

            var aboutAction = Gio.SimpleAction.New("about", null);
            aboutAction.OnActivate += (_, _) => Console.WriteLine("About clicked");
            application.AddAction(aboutAction);

            var configService = serviceProvider.GetRequiredService<IConfigService>();
            var initialConfig = configService.LoadConfig();

            List<IShellyWindow> currentPackagesWindows;
            List<IShellyWindow> currentAurWindows = [];
            IShellyWindow? currentFlatpakWindow = null;
            IShellyWindow? currentAppImageWindow = null;

            void UnloadPage(Box pageBox, IEnumerable<IShellyWindow> windows)
            {
                while (pageBox.GetFirstChild() is { } child)
                {
                    pageBox.Remove(child);
                    child.Unparent();
                }

                foreach (var w in windows)
                    w.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            void LoadPackagesPage()
            {
                var nb = Notebook.New();
                nb.Hexpand = true;
                nb.Vexpand = true;
                var w1 = serviceProvider.GetRequiredService<PackageInstall>();
                nb.AppendPage(w1.CreateWindow(), Label.New("Install"));
                var w2 = serviceProvider.GetRequiredService<PackageUpdate>();
                nb.AppendPage(w2.CreateWindow(), Label.New("Updates"));
                var w3 = serviceProvider.GetRequiredService<PackageManagement>();
                nb.AppendPage(w3.CreateWindow(), Label.New("Manage"));
                packagesPageBox.Append(nb);
                currentPackagesWindows = [w1, w2, w3];
            }

            void LoadAurPage()
            {
                var nb = Notebook.New();
                nb.Hexpand = true;
                nb.Vexpand = true;
                var w1 = serviceProvider.GetRequiredService<AurInstall>();
                nb.AppendPage(w1.CreateWindow(), Label.New("Install"));
                var w2 = serviceProvider.GetRequiredService<AurUpdate>();
                nb.AppendPage(w2.CreateWindow(), Label.New("Updates"));
                var w3 = serviceProvider.GetRequiredService<AurRemove>();
                nb.AppendPage(w3.CreateWindow(), Label.New("Remove"));
                aurPageBox.Append(nb);
                currentAurWindows = [w1, w2, w3];
            }

            void LoadFlatpakPage()
            {
                var w = serviceProvider.GetRequiredService<FlatpakInstall>();
                flatpakPageBox.Append(w.CreateWindow());
                currentFlatpakWindow = w;
            }

            void LoadAppImagePage()
            {
                var w = serviceProvider.GetRequiredService<AppImage>();
                appImagePageBox.Append(w.CreateWindow());
                currentAppImageWindow = w;
            }

            LoadPackagesPage();

            var settingsWindow = serviceProvider.GetRequiredService<Settings>();
            settingsPageBox.Append(settingsWindow.CreateWindow());

            settingsStack.GetPage(aurPageBox).Visible = initialConfig.AurEnabled;
            settingsStack.GetPage(flatpakPageBox).Visible = initialConfig.FlatPackEnabled;
            settingsStack.GetPage(appImagePageBox).Visible = initialConfig.AppImageEnabled;

            settingsWindow.ConfigChanged += (config) =>
            {
                settingsStack.GetPage(aurPageBox).Visible = config.AurEnabled;
                settingsStack.GetPage(flatpakPageBox).Visible = config.FlatPackEnabled;
                settingsStack.GetPage(appImagePageBox).Visible = config.AppImageEnabled;
            };

            var previousPage = settingsStack.GetVisibleChildName();

            settingsStack.OnNotify += (_, notifySignalArgs) =>
            {
                if (notifySignalArgs.Pspec.GetName() != "visible-child-name") return;
                var currentPage = settingsStack.GetVisibleChildName();
                if (currentPage == previousPage) return;

                switch (previousPage)
                {
                    case "packages_page":
                        UnloadPage(packagesPageBox, currentPackagesWindows);
                        currentPackagesWindows = [];
                        break;
                    case "aur_page":
                        UnloadPage(aurPageBox, currentAurWindows);
                        currentAurWindows = [];
                        break;
                    case "flatpak_page":
                        if (currentFlatpakWindow != null)
                        {
                            UnloadPage(flatpakPageBox, [currentFlatpakWindow]);
                            currentFlatpakWindow = null;
                        }

                        break;
                    case "appimage_page":
                        if (currentAppImageWindow != null)
                        {
                            UnloadPage(appImagePageBox, [currentAppImageWindow]);
                            currentAppImageWindow = null;
                        }

                        break;
                }

                switch (currentPage)
                {
                    case "packages_page": LoadPackagesPage(); break;
                    case "aur_page": LoadAurPage(); break;
                    case "flatpak_page": LoadFlatpakPage(); break;
                    case "appimage_page": LoadAppImagePage(); break;
                }

                previousPage = currentPage;
            };


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

            var lockoutDialog = serviceProvider.GetRequiredService<LockoutDialog>();

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

            if (!initialConfig.NewInstallInitSettings)
            {
                var setupWindow = serviceProvider.GetRequiredService<SetupWindow>();
                var setupWidget = setupWindow.CreateWindow();

                mainOverlay.AddOverlay(setupWidget);

                setupWindow.SetupFinished += (_, _) =>
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        mainOverlay.RemoveOverlay(setupWidget);
                        setupWindow.Dispose();
                        return false;
                    });
                };
            }

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

            var historyMenuButton = (MenuButton)mainBuilder.GetObject("history_menu_button")!;
            var historyListBox = (ListBox)mainBuilder.GetObject("history_list_box")!;
            var historyPopoverTitle = (Label)mainBuilder.GetObject("history_popover_title")!;

            BottomBarExtensions.SetupHistoryButton(
                historyMenuButton,
                historyListBox,
                historyPopoverTitle,
                serviceProvider,
                genericQuestionService,
                mainOverlay);

            var updatesMenuButton = (MenuButton)mainBuilder.GetObject("updates_menu_button")!;
            var updatesListBox = (ListBox)mainBuilder.GetObject("updates_list_box")!;
            var updatesPopoverTitle = (Label)mainBuilder.GetObject("updates_popover_title")!;
            var packageUpdateNotifier = serviceProvider.GetRequiredService<IPackageUpdateNotifier>();

            BottomBarExtensions.SetupUpdatesButton(
                updatesMenuButton,
                updatesListBox,
                updatesPopoverTitle,
                serviceProvider,
                packageUpdateNotifier);

            var upgradeAllButton = (Button)mainBuilder.GetObject("upgrade_all_button")!;
            upgradeAllButton.OnClicked += async (_, _) => { await UpgradeAllAsync(); };
            return;

            async Task UpgradeAllAsync()
            {
                upgradeAllButton.Sensitive = false;
                var unprivilegedOperationService = serviceProvider.GetRequiredService<IUnprivilegedOperationService>();
                var privilegedOperationService = serviceProvider.GetRequiredService<IPrivilegedOperationService>();
                try
                {
                    var packagesNeedingUpdate = await unprivilegedOperationService.CheckForApplicationUpdates();

                    if (packagesNeedingUpdate.Aur.Count == 0 && packagesNeedingUpdate.Packages.Count == 0 &&
                        packagesNeedingUpdate.Flatpaks.Count == 0)
                    {
                        var toastArgs = new ToastMessageEventArgs("No packages need to be upgraded");
                        genericQuestionService.RaiseToastMessage(toastArgs);
                        return;
                    }

                    if (!configService.LoadConfig().NoConfirm)
                    {
                        var confirmArgs = new GenericQuestionEventArgs(
                            "Upgrade All Packages?",
                            BottomBarExtensions.BuildUpgradeConfirmationMessage(packagesNeedingUpdate),
                            true
                        );

                        genericQuestionService.RaiseQuestion(confirmArgs);
                        if (!await confirmArgs.ResponseTask)
                        {
                            return;
                        }
                    }

                    lockoutService.Show("Upgrading all packages...");
                    var aurUpdates = packagesNeedingUpdate.Aur;
                    if (aurUpdates.Count != 0)
                    {
                        var aurPackageNames = aurUpdates.Select(p => p.Name).ToList();
                        var packageBuilds = await privilegedOperationService.GetAurPackageBuild(aurPackageNames);
                        foreach (var pkgbuild in packageBuilds)
                        {
                            if (pkgbuild.PkgBuild == null) continue;
                            var buildArgs = new PackageBuildEventArgs($"Displaying Package Build {pkgbuild.Name}",
                                pkgbuild.PkgBuild);
                            genericQuestionService.RaisePackageBuild(buildArgs);
                            if (!await buildArgs.ResponseTask)
                            {
                                return;
                            }
                        }
                    }

                    var upgradeResult = await privilegedOperationService.UpgradeAllAsync();
                    if (upgradeResult.NeedsReboot)
                    {
                        var rebootArgs = new GenericQuestionEventArgs(
                            "Reboot Required",
                            "A full system reboot is required for updates to take effect.\n\nWould you like to reboot now?",
                            true
                        );
                        genericQuestionService.RaiseQuestion(rebootArgs);
                        if (await rebootArgs.ResponseTask)
                        {
                            System.Diagnostics.Process.Start("systemctl", "reboot");
                        }
                    }
                    else if (upgradeResult.FailedServiceRestarts.Count > 0)
                    {
                        var failureList = string.Join("\n", upgradeResult.FailedServiceRestarts
                            .Select(f => $"  • {f.Service}: {f.Error}"));
                        var failArgs = new GenericQuestionEventArgs(
                            "Service Restart Failures",
                            $"The following services failed to restart automatically:\n{failureList}",
                            false
                        );
                        genericQuestionService.RaiseQuestion(failArgs);
                        await failArgs.ResponseTask;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    lockoutService.Hide();
                    upgradeAllButton.Sensitive = true;
                }
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
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.Services.TrayServices;
using Shelly.Gtk.UiModels;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLib;
using Shelly.Gtk.Windows.Dialog;
using DateTime = System.DateTime;
using TimeSpan = System.TimeSpan;


namespace Shelly.Gtk.Windows;

public class Settings(
    IConfigService configService,
    IPrivilegedOperationService privilegedOperationService,
    IUnprivilegedOperationService unprivilegedOperationService,
    ILockoutService lockoutService,
    IGenericQuestionService genericQuestionService) : IShellyWindow
{
    private Box _box = null!;
    private ShellyConfig _config = null!;
    private Overlay? _parentOverlay;
    private static List<ReleaseNotesDialog.ReleaseItem>? _cachedReleaseList = null!;
    private static string? _cachedLatestVersion = null!;
    private static DateTime _lastVersionCheck = DateTime.MinValue;
    private static readonly TimeSpan VersionCheckInterval = TimeSpan.FromMinutes(5);

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { UserAgent = { new("Shelly-ALPM", null) } }
    };

    public event Action? NavigationToHomeRequested;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/SettingWindow.ui"), -1);
        _box = (Box)builder.GetObject("SettingWindow")!;

        _config = configService.LoadConfig();
        _parentOverlay = (Overlay)builder.GetObject("SettingsOverlay")!;

        SetupAurSwitch("aur_switch", _config.AurEnabled, (v) => _config.AurEnabled = v, builder);
        SetupFlatpakSwitch("flatpak_switch", _config.FlatPackEnabled, (v) => _config.FlatPackEnabled = v, builder);
        SetupTraySwitch("tray_switch", _config.TrayEnabled, (v) => _config.TrayEnabled = v, builder);
        SetupWeeklyScheduleSwitch("daily_schedule", _config.UseWeeklySchedule, (v) => _config.UseWeeklySchedule = v,
            builder);
        SetupSwitch("no_confirm_switch", _config.NoConfirm, (v) => _config.NoConfirm = v, builder);
        SetupSwitch("webview_switch", _config.WebViewEnabled, (v) => _config.WebViewEnabled = v, builder);
        SetupSwitch("shelly_icons_switch", _config.ShellyIconsEnabled, (v) => _config.ShellyIconsEnabled = v, builder);
        SetupSwitch("menu_navigation", _config.UseOldMenu, (v) => _config.UseOldMenu = v, builder);
        SetupSwitch("appimage_switch", _config.AppImageEnabled, (v) => _config.AppImageEnabled = v, builder);
        SetupSwitch("symbolic_tray_switch", _config.UseSymbolicTray, (v) => _config.UseSymbolicTray = v, builder);

        var parallelDownloadsSpin = (SpinButton)builder.GetObject("parallel_downloads_spin")!;
        parallelDownloadsSpin.Value = _config.ParallelDownloadCount;
        parallelDownloadsSpin.OnValueChanged += (s, e) =>
        {
            _config.ParallelDownloadCount = (int)parallelDownloadsSpin.Value;
            SaveConfig();
        };

        var traySpin = (SpinButton)builder.GetObject("tray_interval_spin")!;
        traySpin.Value = _config.TrayCheckIntervalHours;
        traySpin.OnValueChanged += (s, e) =>
        {
            _config.TrayCheckIntervalHours = (int)traySpin.Value;
            SaveConfig();
        };

        SetupDayCheckbox("day_sun_check", DayOfWeek.Sunday, builder);
        SetupDayCheckbox("day_mon_check", DayOfWeek.Monday, builder);
        SetupDayCheckbox("day_tue_check", DayOfWeek.Tuesday, builder);
        SetupDayCheckbox("day_wed_check", DayOfWeek.Wednesday, builder);
        SetupDayCheckbox("day_thu_check", DayOfWeek.Thursday, builder);
        SetupDayCheckbox("day_fri_check", DayOfWeek.Friday, builder);
        SetupDayCheckbox("day_sat_check", DayOfWeek.Saturday, builder);

        var hourSpin = (SpinButton)builder.GetObject("update_hour_spin")!;
        var minuteSpin = (SpinButton)builder.GetObject("update_minute_spin")!;

        if (_config.Time.HasValue)
        {
            hourSpin.Value = _config.Time.Value.Hour;
            minuteSpin.Value = _config.Time.Value.Minute;
        }

        hourSpin.OnValueChanged += (s, e) =>
        {
            _config.Time = new TimeOnly((int)hourSpin.Value, (int)minuteSpin.Value);
            SaveConfig();
        };

        minuteSpin.OnValueChanged += (s, e) =>
        {
            _config.Time = new TimeOnly((int)hourSpin.Value, (int)minuteSpin.Value);
            SaveConfig();
        };

        var syncButton = (Button)builder.GetObject("sync_button")!;
        syncButton.OnClicked += (s, e) => { _ = ForceSyncAsync(); };

        var saveButton = (Button)builder.GetObject("save_button")!;
        saveButton.OnClicked += (s, e) => { NavigationToHomeRequested?.Invoke(); };

        var removeLockButton = (Button)builder.GetObject("rm_db_lock_button")!;
        removeLockButton.OnClicked += (s, e) => { _ = RemoveDbLockAsync(); };

        var viewChangelogButton = (Button)builder.GetObject("changelog_button")!;
        viewChangelogButton.OnClicked += async (s, e) => { await ShowAppChangelogAsync(); };

        var purifyCorruptionButton = (Button)builder.GetObject("purify_button")!;
        purifyCorruptionButton.TooltipText = "Remove corrupted packages";
        purifyCorruptionButton.OnClicked += async (s, e) => { await PurifyCorruption(); };

        var fixPermissionsButton = (Button)builder.GetObject("fix_permissions_button")!;
        fixPermissionsButton.OnClicked += async (s, e) => { await FixXdgPermissionsAsync(); };

        var viewPacfilesButton = (Button)builder.GetObject("view_pacfiles_button")!;
        viewPacfilesButton.OnClicked += async (s, e) => { await ViewPacfilesAsync(); };

        var versionLabel = (Label)builder.GetObject("version_label")!;
        versionLabel.SetLabel(
            $"v{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Unknown"}");


        return _box;
    }

    private void SetupSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        sw.Active = initialValue;
        sw.OnStateSet += (s, e) =>
        {
            updateAction(e.State);
            SaveConfig();
            return false;
        };
    }

    private void SetupAurSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        sw.Active = initialValue;
        sw.OnStateSet += (s, e) =>
        {
            if (e.State && !_config.AurWarningConfirmed)
            {
                _ = HandleAurConfirmationAsync(sw, updateAction);
                return true;
            }

            updateAction(e.State);
            SaveConfig();
            return false;
        };
    }

    private void SetupTraySwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        var trayIntervalBox = (Box)builder.GetObject("tray_interval_box")!;
        var symbolicTrayBox = (Box)builder.GetObject("symbolic_tray_box")!;
        var weeklyScheduleSwitchBox = (Box)builder.GetObject("weekly_schedule_switch_box")!;
        var weeklyScheduleBox = (Box)builder.GetObject("weekly_schedule_box")!;
        var weeklyScheduleSwitch = (Switch)builder.GetObject("daily_schedule")!;

        sw.Active = initialValue;

        // Set initial visibility - tray interval is visible only if tray enabled AND weekly schedule disabled
        weeklyScheduleSwitchBox.Visible = initialValue;
        symbolicTrayBox.Visible = initialValue;
        trayIntervalBox.Visible = initialValue && !weeklyScheduleSwitch.Active;
        weeklyScheduleBox.Visible = initialValue && weeklyScheduleSwitch.Active;

        sw.OnStateSet += (s, e) =>
        {
            if (e.State)
            {
                TrayStartService.Start();
            }
            else
            {
                TrayStartService.End();
            }

            weeklyScheduleSwitchBox.Visible = e.State;
            symbolicTrayBox.Visible = e.State;
            trayIntervalBox.Visible = e.State && !weeklyScheduleSwitch.Active;
            weeklyScheduleBox.Visible = e.State && weeklyScheduleSwitch.Active;

            updateAction(e.State);
            SaveConfig();

            return false;
        };
    }

    private void SetupWeeklyScheduleSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        var trayIntervalBox = (Box)builder.GetObject("tray_interval_box")!;
        var weeklyScheduleBox = (Box)builder.GetObject("weekly_schedule_box")!;
        var traySwitch = (Switch)builder.GetObject("tray_switch")!;

        sw.Active = initialValue;

        if (traySwitch.Active)
        {
            trayIntervalBox.Visible = !initialValue;
            weeklyScheduleBox.Visible = initialValue;
        }

        sw.OnStateSet += (s, e) =>
        {
            if (traySwitch.Active)
            {
                trayIntervalBox.Visible = !e.State;
                weeklyScheduleBox.Visible = e.State;
            }

            updateAction(e.State);
            SaveConfig();

            return false;
        };
    }

    private void SetupDayCheckbox(string id, DayOfWeek day, Builder builder)
    {
        var checkbox = (CheckButton)builder.GetObject(id)!;
        checkbox.Active = _config.DaysOfWeek.Contains(day);

        checkbox.OnToggled += (s, e) =>
        {
            if (checkbox.Active)
            {
                if (!_config.DaysOfWeek.Contains(day))
                {
                    _config.DaysOfWeek.Add(day);
                }
            }
            else
            {
                _config.DaysOfWeek.Remove(day);
            }

            SaveConfig();
        };
    }

    private void SetupFlatpakSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        sw.Active = initialValue;
        sw.OnStateSet += (s, e) =>
        {
            if (!e.State)
            {
                updateAction(false);
                SaveConfig();
                return false;
            }

            _ = HandleFlatpakMissingAsync(sw, updateAction);
            return true;
        };
    }

    private void SetupAppImageSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        sw.Active = initialValue;
        sw.OnStateSet += (s, e) =>
        {
            updateAction(e.State);
            SaveConfig();
            return false;
        };
    }


    private async Task HandleAurConfirmationAsync(Switch sw, Action<bool> updateAction)
    {
        var args = new GenericQuestionEventArgs(
            "Enable AUR?",
            "The Arch User Repository (AUR) is a community-driven repository. " +
            "Packages are user-produced and may contain risks. Do you want to enable it?"
        );

        genericQuestionService.RaiseQuestion(args);
        var confirmed = await args.ResponseTask;

        GLib.Functions.IdleAdd(0, () =>
        {
            if (confirmed)
            {
                _config.AurWarningConfirmed = true;
                updateAction(true);
                SaveConfig();
                sw.Active = true;
                sw.State = true;
            }
            else
            {
                sw.Active = false;
                sw.State = false;
            }

            return false;
        });
    }

    private async Task HandleFlatpakMissingAsync(Switch sw, Action<bool> updateAction)
    {
        var result = await privilegedOperationService.IsPackageInstalledOnMachine("flatpak");

        if (!result)
        {
            var args = new GenericQuestionEventArgs(
                "Missing Flatpak",
                "Would you like to install this this now?"
            );

            genericQuestionService.RaiseQuestion(args);
            var confirmed = await args.ResponseTask;

            if (confirmed)
            {
                try
                {
                    lockoutService.Show("Installing flatpak...");
                    await privilegedOperationService.InstallPackagesAsync(["flatpak"]);
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        updateAction(true);
                        SaveConfig();
                        sw.Active = true;
                        sw.State = true;
                        return false;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error installing flatpak: {ex.Message}");
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        sw.Active = false;
                        sw.State = false;
                        return false;
                    });
                }
                finally
                {
                    lockoutService.Hide();
                    genericQuestionService.RaiseToastMessage(
                        new ToastMessageEventArgs("Reboot required to complete installation."));
                }
            }
            else
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    sw.Active = false;
                    sw.State = false;
                    return false;
                });
            }
        }
        else
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                updateAction(true);
                SaveConfig();
                sw.Active = true;
                sw.State = true;
                return false;
            });
        }
    }

    private void SaveConfig()
    {
        configService.SaveConfig(_config);
    }

    private async Task ForceSyncAsync()
    {
        try
        {
            lockoutService.Show("Synchronizing databases...");
            await privilegedOperationService.SyncDatabasesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing databases: {ex.Message}");
        }
        finally
        {
            lockoutService.Hide();
        }
    }

    private async Task RemoveDbLockAsync()
    {
        var result = await privilegedOperationService.RemoveDbLockAsync();

        if (result.Success)
        {
            genericQuestionService.RaiseToastMessage(new ToastMessageEventArgs("Database lock removed"));
        }
        else
        {
            Console.Error.WriteLine($"Failed to remove database lock: {result.Error}");
        }
    }

    private async Task FixXdgPermissionsAsync()
    {
        try
        {
            var result = await privilegedOperationService.FixXdgPermissionsAsync();

            if (result.Success)
            {
                genericQuestionService.RaiseToastMessage(
                    new ToastMessageEventArgs("Shelly folder ownership restored"));
            }
            else
            {
                Console.Error.WriteLine($"Failed to fix Shelly folder ownership: {result.Error}");
                genericQuestionService.RaiseToastMessage(
                    new ToastMessageEventArgs("Failed to fix folder permissions"));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fixing Shelly folder permissions: {ex.Message}");
            genericQuestionService.RaiseToastMessage(
                new ToastMessageEventArgs("Error fixing folder permissions"));
        }
    }

    private async Task PurifyCorruption()
    {
        var result = await privilegedOperationService.PurifyCorruptionAsync();
        if (result.Success)
        {
            var purifyBox = new Box();
            purifyBox.SetOrientation(Orientation.Vertical);
            purifyBox.SetSpacing(12);
            purifyBox.SetSizeRequest(500, -1);
            var title = new Label();
            title.SetText("Purified Corruption");
            title.AddCssClass("title-2");
            title.SetHalign(Align.Center);
            purifyBox.Append(title);
            var scroll = new ScrolledWindow();
            scroll.HscrollbarPolicy = PolicyType.Never;
            scroll.VscrollbarPolicy = PolicyType.Automatic;
            scroll.SetOverlayScrolling(false);
            scroll.SetSizeRequest(-1, 400);
            var list = new Box();
            list.SetOrientation(Orientation.Vertical);
            list.SetSpacing(8);
            var output = result.Output != "\n" ? result.Output.Split(",").ToList() : ["No corrupted packages found"];
            foreach (var pkg in output)
            {
                var text = new Label();
                text.SetText(pkg);
                text.AddCssClass("text");
                list.Append(text);
            }

            scroll.SetChild(list);
            purifyBox.Append(scroll);
            genericQuestionService.RaiseDialog(new GenericDialogEventArgs(purifyBox));
        }
    }

    private async Task ViewPacfilesAsync()
    {
        var pacfiles = await unprivilegedOperationService.GetPacFiles();
        if (pacfiles.Count == 0)
        {
            genericQuestionService.RaiseToastMessage(new ToastMessageEventArgs("No pacfiles found"));
            return;
        }

        var pacfileBox = new Box();
        pacfileBox.SetOrientation(Orientation.Vertical);
        pacfileBox.SetSpacing(12);
        pacfileBox.SetSizeRequest(600, -1);

        var title = new Label();
        title.SetText("Pacfiles Found");
        title.AddCssClass("title-2");
        title.SetHalign(Align.Center);
        pacfileBox.Append(title);

        var scroll = new ScrolledWindow();
        scroll.HscrollbarPolicy = PolicyType.Never;
        scroll.VscrollbarPolicy = PolicyType.Automatic;
        scroll.SetOverlayScrolling(false);
        scroll.SetSizeRequest(-1, 400);

        var list = new Box();
        list.SetOrientation(Orientation.Vertical);
        list.SetSpacing(8);

        foreach (var pacfile in pacfiles)
        {
            var itemBox = new Box();
            itemBox.SetOrientation(Orientation.Vertical);
            itemBox.SetSpacing(4);
            itemBox.SetMarginBottom(16);

            var headerBox = new Box();
            headerBox.SetOrientation(Orientation.Horizontal);
            headerBox.SetSpacing(8);

            var nameLabel = new Label();
            nameLabel.SetMarkup($"<b>{pacfile.Name}</b>");
            nameLabel.SetHalign(Align.Start);
            nameLabel.SetHexpand(true);
            headerBox.Append(nameLabel);

            var copyButton = Button.NewFromIconName("edit-copy-symbolic");
            copyButton.SetTooltipText("Copy content");
            copyButton.AddCssClass("flat");
            copyButton.OnClicked += (_, _) =>
            {
                var clipboard = Gdk.Display.GetDefault().GetClipboard();
                clipboard.SetText(pacfile.Text);
                genericQuestionService.RaiseToastMessage(new ToastMessageEventArgs("Copied to clipboard"));
            };
            headerBox.Append(copyButton);
            
            itemBox.Append(headerBox);

            var textView = new TextView();
            textView.Editable = false;
            textView.CursorVisible = false;
            textView.WrapMode = WrapMode.None;
            textView.Monospace = true;
          
            var buffer = textView.Buffer;
            if (buffer != null)
            {
                var lines = pacfile.Text.Split('\n');
                var builder = new System.Text.StringBuilder();
                for (var i = 0; i < lines.Length; i++)
                {
                    builder.AppendLine($"{(i + 1).ToString().PadLeft(3)} | {lines[i]}");
                }
                buffer.SetText(builder.ToString(), -1);
            }

            var textScroll = new ScrolledWindow();
            textScroll.SetSizeRequest(-1, 200);
            textScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            textScroll.SetChild(textView);
            textScroll.AddCssClass("rounded-card");
            
            itemBox.Append(textScroll);
            list.Append(itemBox);
        }

        scroll.SetChild(list);
        pacfileBox.Append(scroll);
        genericQuestionService.RaiseDialog(new GenericDialogEventArgs(pacfileBox));
    }

    private async Task ShowAppChangelogAsync()
    {
        if (_parentOverlay is null)
        {
            Console.WriteLine("Parent overlay is null");
            genericQuestionService.RaiseToastMessage(
                new ToastMessageEventArgs("Overlay not available"));
            return;
        }

        try
        {
            if (_cachedReleaseList is not null &&
                DateTime.UtcNow - _lastVersionCheck < VersionCheckInterval)
            {
                Console.WriteLine("Cached releases used (skipping version check)");
                ReleaseNotesDialog.ShowReleaseHistoryDialog(_parentOverlay, _cachedReleaseList);
                return;
            }

            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shelly-ALPM");

            var url = "https://api.github.com/repos/Seafoam-Labs/Shelly-ALPM/releases";
            var latestJson = await HttpClient.GetStringAsync($"{url}/latest");
            _lastVersionCheck = DateTime.UtcNow;
            using var latestDoc = JsonDocument.Parse(latestJson);
            var latestTag = latestDoc.RootElement
                .TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString()
                : null;

            if (_cachedReleaseList is not null && latestTag == _cachedLatestVersion)
            {
                Console.WriteLine("Cached Releases used");
                ReleaseNotesDialog.ShowReleaseHistoryDialog(_parentOverlay, _cachedReleaseList);
                return;
            }

            var json = await HttpClient.GetStringAsync(url);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                genericQuestionService.RaiseToastMessage(
                    new ToastMessageEventArgs("No changelog entries found"));
                return;
            }

            var releases = new List<ReleaseNotesDialog.ReleaseItem>();

            foreach (var release in root.EnumerateArray())
            {
                var version = release.TryGetProperty("tag_name", out var tagNameProp)
                    ? tagNameProp.GetString() ?? "Unknown"
                    : "Unknown";

                var markdown = release.TryGetProperty("body", out var bodyProp)
                    ? bodyProp.GetString() ?? "No details for this release"
                    : "No details for this release";

                var publishedAtRaw = release.TryGetProperty("published_at", out var publishedAtProp)
                    ? publishedAtProp.GetString()
                    : null;

                var date = DateTimeOffset.TryParse(publishedAtRaw, out var published)
                    ? published.ToString("yyyy-MM-dd")
                    : "Unknown date";

                releases.Add(new ReleaseNotesDialog.ReleaseItem
                {
                    Version = version,
                    Date = date,
                    Markdown = string.IsNullOrWhiteSpace(markdown)
                        ? "No details for this release"
                        : markdown
                });
            }

            if (releases.Count == 0)
            {
                genericQuestionService.RaiseToastMessage(
                    new ToastMessageEventArgs("No changelog entries found"));
                return;
            }

            _cachedReleaseList = releases;
            _cachedLatestVersion = latestTag;

            ReleaseNotesDialog.ShowReleaseHistoryDialog(_parentOverlay, releases);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading changelog: {ex.Message}");
            genericQuestionService.RaiseToastMessage(
                new ToastMessageEventArgs("Failed to load changelog"));
        }
    }

    private async Task GetAnyShowPacfiles()
    {
        try
        {
            var result = await unprivilegedOperationService.GetPacFiles();
            
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting pacfiles: {ex.Message}");
            genericQuestionService.RaiseToastMessage(
                new ToastMessageEventArgs("Failed to load pacfiles"));
        }
    }


    public void Dispose()
    {
    }
}
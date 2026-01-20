using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels;
using System.Diagnostics;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shelly_UI.Models;

namespace Shelly_UI.Views;

public partial class PackageWindow : ReactiveUserControl<PackageViewModel>
{
    public PackageWindow()
    {
      
        AvaloniaXamlLoader.Load(this);
        this.WhenActivated(disposables =>
        {
            
        });
    }

    private void OpenUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.MenuItem mi)
        {
            var url = mi.CommandParameter as string;
            if (string.IsNullOrWhiteSpace(url)) return;
            OpenUrlCrossPlatform(url!);
        }
    }

    private static void OpenUrlCrossPlatform(string url)
    {
        try
        {
            var targetUser = ResolveDesktopUser();
            if (!string.IsNullOrWhiteSpace(targetUser) &&
                !string.Equals(targetUser, "root", StringComparison.OrdinalIgnoreCase))
            {
                if (TryStartAsUserWithRunUser(targetUser!, url)) return;
            }
        }
        catch (Exception e)
        {
            Console.Write(e);
        }
    }

    private static bool TryStartAsUserWithRunUser(string user, string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "runuser",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
            };
            // runuser -u <user> -- env [DISPLAY=..] [WAYLAND_DISPLAY=..] [XDG_RUNTIME_DIR=..] [DBUS_SESSION_BUS_ADDRESS=..] xdg-open <url>
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(user);
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add("env");

            AddEnvArgIfSet(psi, "DISPLAY");
            AddEnvArgIfSet(psi, "WAYLAND_DISPLAY");
            var uid = GetUidForUser(user);
            if (!string.IsNullOrEmpty(uid))
            {
                psi.ArgumentList.Add($"XDG_RUNTIME_DIR=/run/user/{uid}");
            }

            AddEnvArgIfSet(psi, "DBUS_SESSION_BUS_ADDRESS");

            psi.ArgumentList.Add("xdg-open");
            psi.ArgumentList.Add(url);

            return Process.Start(psi) != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveDesktopUser()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "logname",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(500);
            if (!string.IsNullOrWhiteSpace(output)) return output;
        }
        catch (Exception e)
        {
            Console.Write(e);
        }

        return null;
    }

    private static string? GetUidForUser(string user)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "id",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(user);
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(500);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static void AddEnvArgIfSet(ProcessStartInfo psi, string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(value))
        {
            psi.ArgumentList.Add($"{key}={value}");
        }
    }

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();

        if (row?.DataContext is not PackageModel package) return;
        if (DataContext is not PackageViewModel vm) return;


        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}
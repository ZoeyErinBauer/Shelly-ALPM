using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PackageManager.Alpm;
using PackageManager.User;
using Avalonia.Threading;
using Shelly_UI.ViewModels;
using Shelly_UI.Views;

namespace Shelly_UI.Services;

public static class AlpmService
{
    private static IAlpmManager? _instance;
    private static readonly IPasswordService _passwordService = new PasswordService();

    public static IAlpmManager Instance
    {
        get
        {
            if (_instance == null)
            {
                if (UserIdentity.IsRoot())
                {
                    _instance = new AlpmManager();
                }
                else
                {
                    string workerPath = GetWorkerPath();
                    _instance = new AlpmWorkerClient(workerPath, GetPassword);
                }
            }
            return _instance;
        }
    }

    private static string? GetPassword()
    {
        if (_passwordService.HasPassword())
        {
            return _passwordService.GetPassword();
        }

        // We need to show the dialog on the UI thread and wait for the result.
        var password = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            return await ShowPasswordDialog();
        }).GetAwaiter().GetResult();

        if (password != null)
        {
            _passwordService.SetPassword(password);
        }
        return password;
    }

    private static async Task<string?> ShowPasswordDialog()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new PasswordWindow
            {
                DataContext = new PasswordViewModel()
            };
            
            // If the main window is not yet visible or available, we might have issues.
            // But usually, it is available when worker calls are made.
            var owner = desktop.MainWindow;
            if (owner != null)
            {
                return await dialog.ShowDialog<string?>(owner);
            }
            else
            {
                // Fallback if no main window (unlikely in this app)
                return await dialog.ShowDialog<string?>(new Window());
            }
        }
        return null;
    }

    private static string GetWorkerPath()
    {
        // In development, the worker might be in a different place.
        // We'll try a few common locations.
        string workerName = "Shelly.Worker";
        string[] searchPaths = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, workerName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Shelly.Worker", "bin", "Debug", "net10.0", workerName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Shelly.Worker", "bin", "Release", "net10.0", workerName),
            Path.Combine("/usr/lib/shelly", workerName)
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fallback to expecting it in PATH or current directory
        return workerName;
    }
}

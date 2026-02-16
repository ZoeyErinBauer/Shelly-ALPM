using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using PackageManager;
using PackageManager.Alpm;
using PackageManager.Aur;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Utility;

public class Export : AsyncCommand<ExportSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] ExportSettings settings)
    {
        var username = Environment.GetEnvironmentVariable("USER");

        if (username == "root" || string.IsNullOrEmpty(username))
        {
            throw new InvalidOperationException(
                "Cannot determine non-root user for cache directory."
            );
        }
        
        var time = DateTimeOffset.Now;
        
        var path = string.IsNullOrEmpty(settings.Output)
            ? Path.Combine("/home", username, ".cache", "Shelly", string.IsNullOrEmpty(settings.Name) ? $"{time:yyyyMMddHHmmss}_shelly.sync" : settings.Name + ".sync")
            : Path.Combine(settings.Output,  string.IsNullOrEmpty(settings.Name) ? $"{time:yyyyMMddHHmmss}_shelly.sync" : settings.Name + ".sync");

        //Alpm 
        using var manager = new AlpmManager();
        var packages = manager.GetInstalledPackages();

        //Aur
        AurPackageManager? AurManager = null;
        AurManager = new AurPackageManager();
        await AurManager.Initialize();
        var aurPackages = await AurManager.GetInstalledPackages();
        AurManager.Dispose();

        //Flatpaks
        var flatpak = new FlatpakManager();
        var flatpaks = flatpak.SearchInstalled();

        var syncModel = new SyncModel
        {
            MetaData = new SyncMetaData
            {
                Date = time.ToString("yyyy-MM-dd"),
                Time = time.ToUnixTimeSeconds()
            },
            Packages = packages.Select(x => new SyncPackageModel { Name = x.Name, Version = x.Version }).ToList(),
            Aur = aurPackages.Select(x => new SyncAurModel { Name = x.Name, Version = x.Version }).ToList(),
            Flatpaks = flatpaks.Select(x => new SyncFlatpakModel { Id = x.Id, Version = x.Version }).ToList()
        };

        var json = JsonSerializer.Serialize(syncModel, ShellyCLIJsonContext.Default.SyncModel);

        Console.WriteLine(json);

        await File.WriteAllTextAsync(path, json);

        AnsiConsole.MarkupLine($"[blue]Sync file exported to: {path}[/]");

        return 0;
    }
}
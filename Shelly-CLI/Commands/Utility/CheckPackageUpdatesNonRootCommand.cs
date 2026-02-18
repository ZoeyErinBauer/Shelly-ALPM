using System.Text.Json;
using PackageManager.Alpm;
using PackageManager.Aur;
using PackageManager.Aur.Models;
using PackageManager.Flatpak;
using Shelly_CLI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Utility;

public class CheckPackageUpdatesNonRootCommand : AsyncCommand<CheckPackageUpdatesNonRootSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CheckPackageUpdatesNonRootSettings settings)
    {
        if (Program.IsUiMode)
        {
            return await HandleUiModeCheckUpdates(settings);
        }
        var alpmManager = new AlpmManager();
        List<AlpmPackageUpdateDto> alpmPackages = [];
        var aurManager = new AurPackageManager();
        List<AurUpdateDto> aurPackages = [];
        var flatPakManager = new FlatpakManager();
        List<FlatpakPackageDto> flatpakPackages = [];
        var username = Environment.GetEnvironmentVariable("USER");
        var dbPath = Path.Combine("/home", username, ".cache", "Shelly", "db");
        Directory.CreateDirectory(dbPath);
        AnsiConsole.WriteLine(dbPath);
        if (settings.JsonOutput)
        {
            alpmManager.Initialize(false, true, dbPath);
            alpmManager.Sync();
            alpmPackages = alpmManager.GetPackagesNeedingUpdate();
            alpmManager.Dispose();
            var syncModel = new SyncModel();
            List<SyncPackageModel> syncPackageModels = [];
            syncPackageModels.AddRange(alpmPackages.Select(pkg => new SyncPackageModel()
            {
                Name = pkg.Name, DownloadSize = FormatSize(pkg.DownloadSize), OldVersion = pkg.CurrentVersion,
                Version = pkg.NewVersion
            }));

            syncModel.Packages = syncPackageModels;
            if (settings.CheckAur)
            {
                aurManager.Initialize(false, true, dbPath);
                aurPackages = await aurManager.GetPackagesNeedingUpdate();
                aurManager.Dispose();
                List<SyncAurModel> aurPackageModels = [];
                aurPackageModels.AddRange(aurPackages.Select(pkg => new SyncAurModel()
                    { Name = pkg.Name, OldVersion = pkg.Version, Version = pkg.NewVersion }));
                syncModel.Aur = aurPackageModels;
            }

            if (settings.CheckFlatpak)
            {
                flatpakPackages = flatPakManager.GetPackagesWithUpdates();
                List<SyncFlatpakModel> flatpakPackageModels = [];
                flatpakPackageModels.AddRange(flatpakPackages.Select(pkg => new SyncFlatpakModel()
                    { Id = pkg.Id, Name = pkg.Name, Version = pkg.Version }));
                syncModel.Flatpaks = flatpakPackageModels;
            }

            var json = JsonSerializer.Serialize(syncModel, ShellyCLIJsonContext.Default.SyncModel);
            // Write directly to stdout stream to bypass Spectre.Console redirection
            await using var stdout = Console.OpenStandardOutput();
            await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync(json);
            await writer.FlushAsync();
            return 0;
        }

        AnsiConsole.Status().Spinner(Spinner.Known.BouncingBall).Start("Initializing and syncing ALPM updates",
            ctx =>
            {
                alpmManager.Initialize(false, true, dbPath);
                alpmManager.Sync();
                alpmPackages = alpmManager.GetPackagesNeedingUpdate();
                alpmManager.Dispose();
            });
        AnsiConsole.MarkupLine("[green]Finished checking Standard[/]");
        if (settings.CheckAur)
        {
            AnsiConsole.Status().Spinner(Spinner.Known.BouncingBall).Start("Initializing and syncing AUR packages",
                async ctx =>
                {
                    aurManager.Initialize(false, true, dbPath);
                    aurPackages = await aurManager.GetPackagesNeedingUpdate();
                    aurManager.Dispose();
                });
            AnsiConsole.MarkupLine("[green]Finished checking AUR[/]");
        }

        if (settings.CheckFlatpak)
        {
            AnsiConsole.Status().Spinner(Spinner.Known.BouncingBall).Start(
                "Initializing and syncing Flatpak packages",
                ctx => { flatpakPackages = flatPakManager.GetPackagesWithUpdates(); });
            AnsiConsole.MarkupLine("[green]Finished checking Flatpak[/]");
        }

        var table = new Table().AddColumns("Name", "Type", "New Version", "Current Version", "Download Size");
        foreach (var alpm in alpmPackages)
        {
            table.AddRow(alpm.Name, "Standard", alpm.NewVersion, alpm.CurrentVersion,
                FormatSize(alpm.DownloadSize));
        }

        foreach (var pkg in aurPackages)
        {
            table.AddRow(pkg.Name, "AUR", pkg.NewVersion, pkg.Version, FormatSize(pkg.DownloadSize));
        }

        foreach (var pkg in flatpakPackages)
        {
            table.AddRow(pkg.Name, "Flatpak", pkg.Version, "", "");
        }

        AnsiConsole.Write(table);


        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static async Task<int> HandleUiModeCheckUpdates(CheckPackageUpdatesNonRootSettings settings)
    {
        var alpmManager = new AlpmManager();
        List<AlpmPackageUpdateDto> alpmPackages = [];
        var aurManager = new AurPackageManager();
        List<AurUpdateDto> aurPackages = [];
        var flatPakManager = new FlatpakManager();
        List<FlatpakPackageDto> flatpakPackages = [];
        var username = Environment.GetEnvironmentVariable("USER");
        var dbPath = Path.Combine("/home", username, ".cache", "Shelly", "db");
        Directory.CreateDirectory(dbPath);
        Console.Error.WriteLine(dbPath);
        
        if (settings.JsonOutput)
        {
            alpmManager.Initialize(false, true, dbPath);
            alpmManager.Sync();
            alpmPackages = alpmManager.GetPackagesNeedingUpdate();
            alpmManager.Dispose();
            var syncModel = new SyncModel();
            List<SyncPackageModel> syncPackageModels = [];
            syncPackageModels.AddRange(alpmPackages.Select(pkg => new SyncPackageModel()
            {
                Name = pkg.Name, DownloadSize = FormatSize(pkg.DownloadSize), OldVersion = pkg.CurrentVersion,
                Version = pkg.NewVersion
            }));

            syncModel.Packages = syncPackageModels;
            if (settings.CheckAur)
            {
                aurManager.Initialize(false, true, dbPath);
                aurPackages = await aurManager.GetPackagesNeedingUpdate();
                aurManager.Dispose();
                List<SyncAurModel> aurPackageModels = [];
                aurPackageModels.AddRange(aurPackages.Select(pkg => new SyncAurModel()
                    { Name = pkg.Name, OldVersion = pkg.Version, Version = pkg.NewVersion }));
                syncModel.Aur = aurPackageModels;
            }

            if (settings.CheckFlatpak)
            {
                flatpakPackages = flatPakManager.GetPackagesWithUpdates();
                List<SyncFlatpakModel> flatpakPackageModels = [];
                flatpakPackageModels.AddRange(flatpakPackages.Select(pkg => new SyncFlatpakModel()
                    { Id = pkg.Id, Name = pkg.Name, Version = pkg.Version }));
                syncModel.Flatpaks = flatpakPackageModels;
            }

            var json = JsonSerializer.Serialize(syncModel, ShellyCLIJsonContext.Default.SyncModel);
            await using var stdout = Console.OpenStandardOutput();
            await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync(json);
            await writer.FlushAsync();
            return 0;
        }

        Console.Error.WriteLine("Initializing and syncing ALPM updates");
        alpmManager.Initialize(false, true, dbPath);
        alpmManager.Sync();
        alpmPackages = alpmManager.GetPackagesNeedingUpdate();
        alpmManager.Dispose();
        Console.Error.WriteLine("Finished checking Standard");
        
        if (settings.CheckAur)
        {
            Console.Error.WriteLine("Initializing and syncing AUR packages");
            aurManager.Initialize(false, true, dbPath);
            aurPackages = await aurManager.GetPackagesNeedingUpdate();
            aurManager.Dispose();
            Console.Error.WriteLine("Finished checking AUR");
        }

        if (settings.CheckFlatpak)
        {
            Console.Error.WriteLine("Initializing and syncing Flatpak packages");
            flatpakPackages = flatPakManager.GetPackagesWithUpdates();
            Console.Error.WriteLine("Finished checking Flatpak");
        }

        foreach (var alpm in alpmPackages)
        {
            Console.WriteLine($"{alpm.Name} Standard {alpm.NewVersion} {alpm.CurrentVersion} {FormatSize(alpm.DownloadSize)}");
        }

        foreach (var pkg in aurPackages)
        {
            Console.WriteLine($"{pkg.Name} AUR {pkg.NewVersion} {pkg.Version} {FormatSize(pkg.DownloadSize)}");
        }

        foreach (var pkg in flatpakPackages)
        {
            Console.WriteLine($"{pkg.Name} Flatpak {pkg.Version}");
        }

        return 0;
    }
}
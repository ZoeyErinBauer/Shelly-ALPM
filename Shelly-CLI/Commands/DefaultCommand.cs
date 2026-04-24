using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PackageManager.Alpm;
using PackageManager.Aur;
using PackageManager.Aur.Models;
using PackageManager.Utilities;
using Shelly_CLI.Commands.Aur;
using Shelly_CLI.Commands.Flatpak;
using Shelly_CLI.Commands.Standard;
using Shelly_CLI.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class DefaultCommand : AsyncCommand<DefaultCommandSettings>
{
    //No Confirm is not implemented for default command by design
    public override async Task<int> ExecuteAsync(CommandContext context, [NotNull] DefaultCommandSettings settings)
    {
        
        if (settings.Version)
        {
            new VersionCommand().Execute(context);
            return 0;
        }
        
        var configPath = XdgPaths.ShellyConfig("config.json");
        if (!File.Exists(configPath))
        {
            return 1;
        }

        var json = await File.ReadAllTextAsync(configPath);

        var config = JsonSerializer.Deserialize<ShellyConfig>(json, ShellyCLIJsonContext.Default.ShellyConfig);
        if (config == null)
        {
            return 1;
        }

        var exitOption = "Exit";
        
        if (!string.IsNullOrEmpty(settings.SearchString))
        {
            RootElevator.EnsureRootExectuion();
            var standard = SearchStandard(settings.SearchString);
            var aur = SearchAur(settings.SearchString);
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[yellow]Found {standard.Count + aur.Count} packages please select one to install[/]")
                    .AddChoices(standard.Select(x => $"{x.Name} : {x.Version} : Standard"))
                    .AddChoices(aur.Select(x => $"{x.Name} : {x.Version} : AUR"))
                    .AddChoices(exitOption)
                    .UseConverter(choice => choice == exitOption ? $"[yellow]{exitOption}[/]" : choice));
            if (selection == exitOption)
            {
                return 0;
            }
            var selectionArray = selection.Split(":");
            var name = selectionArray[0].Trim();
            Console.WriteLine(name);
            var repo = selectionArray[2].Trim();
            if (repo == "AUR")
            {
                new Aur.AurInstallCommand().ExecuteAsync(context, new AurInstallSettings()
                {
                    Packages = [name]
                }).Wait();
            }
            else
            {
                new InstallCommand().ExecuteAsync(context, new InstallPackageSettings()
                {
                    Packages = [name]
                }).Wait();
            }
            
            return 0;
        }

        var parsed =
            (Shelly_CLI.Configuration.DefaultCommand)Enum.Parse(typeof(Shelly_CLI.Configuration.DefaultCommand),
                config.DefaultExecution);
        return parsed switch
        {
            Shelly_CLI.Configuration.DefaultCommand.UpgradeStandard => new UpgradeCommand().ExecuteAsync(context,
                new UpgradeSettings()).Result,
            Shelly_CLI.Configuration.DefaultCommand.UpgradeFlatpak => new FlatpakUpgrade().Execute(context),
            Shelly_CLI.Configuration.DefaultCommand.UpgradeAur => await new AurUpgradeCommand().ExecuteAsync(context,
                new AurUpgradeSettings()),
            Shelly_CLI.Configuration.DefaultCommand.UpgradeAll => new UpgradeCommand().ExecuteAsync(context,
                new UpgradeSettings { All = true }).Result,
            Shelly_CLI.Configuration.DefaultCommand.Sync => new SyncCommand().Execute(context, new SyncSettings()),
            Shelly_CLI.Configuration.DefaultCommand.SyncForce => new SyncCommand().Execute(context,
                new SyncSettings { Force = true }),
            Shelly_CLI.Configuration.DefaultCommand.ListInstalled => new ListInstalledCommand().Execute(context,
                new ListSettings()),
            _ => 1
        };
    }

    private List<AlpmPackageDto> SearchStandard(string filter)
    {
        var manager = new AlpmManager();
        manager.Initialize();
        var packages = manager.GetAvailablePackages();
        packages = packages.Select(x => new { Package = x, Score = StringMatching.PartialRatio(filter, x.Name) })
            .Where(x => x.Score >= 75)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Package)
            .Take(5) //Add configuration here to control amount visible.
            .ToList();
        manager.Dispose();
        return packages;
    }

    private List<AurPackageDto> SearchAur(string filter)
    {
        var manager = new AurPackageManager();
        manager.Initialize().Wait();
        var packages = manager.SearchPackages(filter).Result;
        packages = packages.Select(x => new { Package = x, Score = StringMatching.PartialRatio(filter, x.Name) })
            .Where(x => x.Score >= 75)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Package)
            .Take(5) //Add configuration here to control amount visible.
            .ToList();
        manager.Dispose();
        return packages;
    }
}
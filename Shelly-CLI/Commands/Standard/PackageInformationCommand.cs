using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class PackageInformationCommand : Command<PackageInformationSettings>
{
    public override int Execute(CommandContext context, PackageInformationSettings settings)
    {
        if (Program.IsUiMode || settings.JsonOutput)
        {
            Console.WriteLine("Not supported for ui methods yet");
            return 0;
        }

        if (settings.Packages.Length > 1)
        {
            Console.WriteLine("Only one package at a time is currently supported.");
            return 0;
        }

        var manager = new AlpmManager();
        AlpmPackageDto? package = null;
        if (settings.SearchInstalled)
        {
            var installedPackages = manager.GetInstalledPackages();
            package = installedPackages.FirstOrDefault(x => x.Name == settings.Packages[0]);
        }
        else if (settings.SearchRepository)
        {
            var available = manager.GetAvailablePackages();
            package = available.FirstOrDefault(x => x.Name == settings.Packages[0]);
        }
        else
        {
            Console.WriteLine("No search source specified");
            return 0;
        }

        if (package is null)
        {
            AnsiConsole.MarkupLine($"[red]No package named {settings.Packages[0]} found[/]");
            return 0;
        }

        WriteLeftAlignMarkup($"[green]Name: {package.Name}[/]");
        WriteLeftAlignMarkup($"[blue]Version {package.Version}[/]");
        WriteLeftAlignMarkup($"[blue]Description: {package.Description}[/]");
        WriteLeftAlignMarkup($"[blue]URL: {package.Url}[/]");
        WriteLeftAlignMarkup($"[blue]Licenses: {string.Join(',', package.Licenses)}[/]");
        WriteLeftAlignMarkup($"[blue]Groups: {string.Join(',', package.Groups)}[/]");
        WriteLeftAlignMarkup($"[blue]Provides: {string.Join(',', package.Provides)}[/]");
        WriteLeftAlignMarkup($"[blue]Depends On: {string.Join(',', package.Depends)}[/]");
        WriteLeftAlignMarkup($"[blue]Optional Depends: {string.Join(',', package.OptDepends)}[/]");
        WriteLeftAlignMarkup($"[blue]Required By: {string.Join(',', package.RequiredBy)}[/]");
        WriteLeftAlignMarkup($"[blue]Conflicts With: {string.Join(',', package.Conflicts)}[/]");
        WriteLeftAlignMarkup($"[blue]Replaces: {string.Join(',', package.Replaces)}[/]");
        WriteLeftAlignMarkup($"[blue]Installed Size:{package.InstalledSize} bytes[/]");
        var installDate = package.InstallDate.HasValue
            ? package.InstallDate.Value.ToLongDateString()
            : "Not Installed";
        WriteLeftAlignMarkup($"[blue]Install Date: {installDate}[/]");
        WriteLeftAlignMarkup($"[blue]Install Reason: {package.InstallReason}[/]");
        return 0;
    }

    private static void WriteLeftAlignMarkup(string value)
    {
        AnsiConsole.Write(new Align(new Markup(value), HorizontalAlignment.Left));
    }
}
using Shelly_CLI.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI;

public class Program
{
    public static int Main(string[] args)
    {
        // Check if running in UI mode (--ui-mode flag passed by Shelly-UI)
        var argsList = args.ToList();
        var isUiMode = argsList.Remove("--ui-mode");
        args = argsList.ToArray();

        if (isUiMode)
        {
            // Configure stderr to use prefix for UI integration
            var stderrWriter = new StderrPrefixWriter(Console.Error);
            Console.SetError(stderrWriter);

            // Configure AnsiConsole to use DualOutputWriter for UI integration
            var dualWriter = new DualOutputWriter(Console.Out, stderrWriter);
            Console.SetOut(dualWriter);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(dualWriter)
            });
        }
        else
        {
            // When running from terminal, filter out lines containing [bracketed] patterns
            var filteringStdout = new FilteringTextWriter(Console.Out);
            var filteringStderr = new FilteringTextWriter(Console.Error);
            Console.SetOut(filteringStdout);
            Console.SetError(filteringStderr);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(filteringStdout)
            });
        }

        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("shelly");
            config.SetApplicationVersion("1.2.4");

            config.AddCommand<VersionCommand>("version")
                .WithDescription("Display the application version");

            config.AddCommand<SyncCommand>("sync")
                .WithDescription("Synchronize package databases");

            config.AddCommand<ListInstalledCommand>("list-installed")
                .WithDescription("List all installed packages");

            config.AddCommand<ListAvailableCommand>("list-available")
                .WithDescription("List all available packages");

            config.AddCommand<ListUpdatesCommand>("list-updates")
                .WithDescription("List packages that need updates");

            config.AddCommand<InstallCommand>("install")
                .WithDescription("Install one or more packages");

            config.AddCommand<RemoveCommand>("remove")
                .WithDescription("Remove one or more packages");

            config.AddCommand<UpdateCommand>("update")
                .WithDescription("Update one or more packages");

            config.AddCommand<UpgradeCommand>("upgrade")
                .WithDescription("Perform a full system upgrade");

            config.AddBranch("keyring", keyring =>
            {
                keyring.SetDescription("Manage pacman keyring");

                keyring.AddCommand<KeyringInitCommand>("init")
                    .WithDescription("Initialize the pacman keyring");

                keyring.AddCommand<KeyringPopulateCommand>("populate")
                    .WithDescription("Reload keys from keyrings in /usr/share/pacman/keyrings");

                keyring.AddCommand<KeyringRecvCommand>("recv")
                    .WithDescription("Receive keys from a keyserver");

                keyring.AddCommand<KeyringLsignCommand>("lsign")
                    .WithDescription("Locally sign the specified key(s)");

                keyring.AddCommand<KeyringListCommand>("list")
                    .WithDescription("List all keys in the keyring");

                keyring.AddCommand<KeyringRefreshCommand>("refresh")
                    .WithDescription("Refresh keys from the keyserver");
            });

            config.AddBranch("aur", aur =>
            {
                aur.SetDescription("Manage AUR packages");

                aur.AddCommand<Aur.AurSearchCommand>("search")
                    .WithDescription("Search for AUR packages");

                aur.AddCommand<Aur.AurListInstalledCommand>("list")
                    .WithDescription("List installed AUR packages");

                aur.AddCommand<Aur.AurListUpdatesCommand>("list-updates")
                    .WithDescription("List AUR packages that need updates");

                aur.AddCommand<Aur.AurInstallCommand>("install")
                    .WithDescription("Install AUR packages");

                aur.AddCommand<Aur.AurUpdateCommand>("update")
                    .WithDescription("Update specific AUR packages");

                aur.AddCommand<Aur.AurUpgradeCommand>("upgrade")
                    .WithDescription("Upgrade all AUR packages");

                aur.AddCommand<Aur.AurRemoveCommand>("remove")
                    .WithDescription("Remove AUR packages");
            });

            config.AddBranch("flatpak", flatpak =>
            {
                flatpak.SetDescription("Manage flatpak");

                flatpak.AddCommand<Flatpak.FlatpakInstallCommand>("install")
                    .WithDescription("Install flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakUpdateCommand>("update")
                    .WithDescription("Update flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakListCommand>("list")
                    .WithDescription("List installed flatpak apps");
                
                flatpak.AddCommand<Flatpak.FlatpakListUpdatesCommand>("list-updates")
                    .WithDescription("List installed flatpak apps");
                
                flatpak.AddCommand<Flatpak.FlatpakRunningCommand>("running")
                    .WithDescription("List running flatpak apps");

                flatpak.AddCommand<Flatpak.FlatpakRemoveCommand>("uninstall")
                    .WithDescription("Remove flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakRunCommand>("run")
                    .WithDescription("Run flatpak app");

                flatpak.AddCommand<Flatpak.FlatpakKillCommand>("kill")
                    .WithDescription("Kill running flatpak app");

                flatpak.AddCommand<Flatpak.FlathubSearchCommand>("search")
                    .WithDescription("Search flatpak");
            });
        });

        return app.Run(args);
    }
}

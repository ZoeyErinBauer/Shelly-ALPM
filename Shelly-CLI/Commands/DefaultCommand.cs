using System.Text.Json;
using Shelly_CLI.Commands.Aur;
using Shelly_CLI.Commands.Flatpak;
using Shelly_CLI.Commands.Standard;
using Shelly_CLI.Configuration;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class DefaultCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var username = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;;
        var configPath = Path.Combine("/home", username, ".config", "shelly", "config.json");
        Console.WriteLine(configPath);
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

        var parsed =
            (Shelly_CLI.Configuration.DefaultCommand)Enum.Parse(typeof(Shelly_CLI.Configuration.DefaultCommand),
                config.DefaultExecution);
        return parsed switch
        {
            Shelly_CLI.Configuration.DefaultCommand.UpgradeStandard => new UpgradeCommand().Execute(context,
                new UpgradeSettings()),
            Shelly_CLI.Configuration.DefaultCommand.UpgradeFlatpak => new FlatpakUpgrade().Execute(context),
            Shelly_CLI.Configuration.DefaultCommand.UpgradeAur => await new AurUpgradeCommand().ExecuteAsync(context,
                new AurUpgradeSettings()),
            Shelly_CLI.Configuration.DefaultCommand.UpgradeAll => new UpgradeCommand().Execute(context,
                new UpgradeSettings { All = true }),
            Shelly_CLI.Configuration.DefaultCommand.Sync => new SyncCommand().Execute(context, new SyncSettings()),
            Shelly_CLI.Configuration.DefaultCommand.SyncForce => new SyncCommand().Execute(context,
                new SyncSettings { Force = true }),
            Shelly_CLI.Configuration.DefaultCommand.ListInstalled => new ListInstalledCommand().Execute(context,
                new ListSettings()),
            _ => 1
        };
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Shelly_CLI.Configuration;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Config;

public class SetParallelDownloads : Command<SetParallelDownloadsSettings>
{
    public override int Execute(CommandContext context, [NotNull] SetParallelDownloadsSettings settings)
    {
        var username = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        ;
        var configPath = Path.Combine("/home", username, ".config", "shelly", "config.json");
        Console.WriteLine(configPath);
        if (!File.Exists(configPath))
        {
            var result = new Restore().ExecuteAsync(context).Result;
            if (result != 0)
            {
                return 1;
            }
        }

        var json = File.ReadAllText(configPath);

        var config = JsonSerializer.Deserialize<ShellyConfig>(json, ShellyCLIJsonContext.Default.ShellyConfig);
        if (config == null)
        {
            var result = new Restore().ExecuteAsync(context).Result;
            if (result != 0)
            {
                return 1;
            }
        }

        var defaultConfig = new ShellyConfig();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonOut = JsonSerializer.Serialize(defaultConfig, typeof(ShellyConfig), new ShellyCLIJsonContext(options));
        File.WriteAllText(configPath, jsonOut);
        return 0;
    }
}
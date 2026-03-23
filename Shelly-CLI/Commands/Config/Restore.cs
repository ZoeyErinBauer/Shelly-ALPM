using System.Text.Json;
using Shelly_CLI.Configuration;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Config;

public class Restore : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var username = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        ;
        var configPath = Path.Combine("/home", username, ".config", "shelly", "config.json");
        if (!File.Exists(configPath))
        {
            await CreateConfigFile(configPath);
            return 0;
        }

        File.Delete(configPath);
        await CreateConfigFile(configPath);
        return 0;
    }

    private static async Task CreateConfigFile(string configPath)
    {
        var file = File.CreateText(configPath);
        var json = JsonSerializer.Serialize<ShellyConfig>(new ShellyConfig(),
            ShellyCLIJsonContext.Default.ShellyConfig);
        await file.WriteAsync(json);
        file.Close();
    }
}
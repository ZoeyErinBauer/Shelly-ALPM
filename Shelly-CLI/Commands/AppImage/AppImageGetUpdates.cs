using PackageManager.AppImage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class AppImageGetUpdates : AsyncCommand<AppImageDefaultSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppImageDefaultSettings settings)
    {
        var manager = new AppImageManager();
        manager.ErrorEvent += (_, args) => { AnsiConsole.MarkupLine($"[red]{args.Error.EscapeMarkup()}[/]"); };

        manager.MessageEvent += (_, args) => { AnsiConsole.MarkupLine($"[blue]{args.Message.EscapeMarkup()}[/]"); };

        var result = await manager.CheckForAppImageUpdates();

        if (settings.Json)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result,
                ShellyCLIJsonContext.Default.ListAppImageUpdateDto);
            await using var stdout = System.Console.OpenStandardOutput();
            await using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync(json);
            await writer.FlushAsync();
        }
        else
        {
            foreach (var update in result)
            {
                AnsiConsole.MarkupLine($"[green]{update.Name} {update.Version} is available[/]");
            }

            if (result.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No updates available[/]");
            }
        }

        return 0;
    }
}
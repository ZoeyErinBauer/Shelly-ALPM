using PackageManager.AppImage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class AppImageGetUpdates : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var manager = new AppImageManager();
        manager.ErrorEvent += (_, args) =>
        {
            AnsiConsole.MarkupLine($"[red]{args.Error.EscapeMarkup()}[/]");
        };

        manager.MessageEvent += (_, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.Message.EscapeMarkup()}[/]");
        };

        var result = await manager.CheckForAppImageUpdates();

        foreach (var update in result)
        {
            AnsiConsole.MarkupLine($"[green]{update.Name} {update.Version} is available[/]");
        }
        
        if (result.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No updates available[/]");
        }
        
        return 0;
    }
}
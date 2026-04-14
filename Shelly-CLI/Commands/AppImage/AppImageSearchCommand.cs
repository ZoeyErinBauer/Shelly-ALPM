using PackageManager.AppImage;
using Shelly_CLI.Commands.Standard;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.AppImage;

public class AppImageSearchCommand : AsyncCommand<AppImageSearchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppImageSearchSettings settings)
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

        var appImages = await manager.GetAppImagesFromLocalDb();
        List<AppImageDto> results;

        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            var query = settings.Query.ToLowerInvariant();
            results = appImages
                .Where(a => a.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase) ||
                            a.DesktopName.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }
        else
        {
            results = appImages;
        }


        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No matching AppImages found in local database.[/]");

            return 0;
        }
        
        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Update URL");

        foreach (var app in results)
        {
            table.AddRow(
                app.Name,
                app.Version,
                app.UpdateURl
            );
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
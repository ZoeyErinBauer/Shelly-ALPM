using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlathubSearchCommand : AsyncCommand<FlathubSearchSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] FlathubSearchSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            AnsiConsole.MarkupLine("[red]Query cannot be empty.[/]");
            return 1;
        }

        try
        {
            var manager = new FlatpakManager();
            if (settings.NoUi)
            {
                var results = await manager.SearchFlathubJsonAsync(
                        settings.Query, page: settings.Page,
                        limit: settings.Limit, ct: CancellationToken.None);
                AnsiConsole.MarkupLine($"[grey]Response JSON:[/] {results.EscapeMarkup()}");
            }
            else
            {
                var results = await manager.SearchFlathubAsync(
                        settings.Query,
                        page: settings.Page,
                        limit: settings.Limit,
                        ct: CancellationToken.None);

                Render(results, settings.Limit);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Search failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private static void Render(FlatpakApiResponse root, int limit)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("AppId");
        table.AddColumn("Summary");

        var count = 0;
        if (root.hits is not null)
        {
            foreach (var item in root.hits)
            {
                if (count++ >= limit) break;

                table.AddRow(
                    item.name.EscapeMarkup(),
                    item.app_id.EscapeMarkup(),
                    item.summary.EscapeMarkup().Truncate(70)
                );
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            $"[blue]Shown:[/] {Math.Min(limit, root.hits.Count)} / [blue]Total Pages:[/] {root.totalPages} / [blue]Current Page:[/] {root.page} / [blue]Total hits:[/] {root.totalHits}");
    }
}
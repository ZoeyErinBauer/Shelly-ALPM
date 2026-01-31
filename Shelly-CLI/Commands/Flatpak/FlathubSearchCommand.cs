using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Flatpak;

public class FlathubSearchCommand : Command<FlathubSearchSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] FlathubSearchSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            AnsiConsole.MarkupLine("[red]Query cannot be empty.[/]");
            return 1;
        }

        try
        {
            var manager = new FlatpakManager();
            if (settings.noUi)
            {
                var results = manager.SearchFlathubJsonAsync(
                        settings.Query, page: settings.Page,
                        limit: settings.Limit, ct: CancellationToken.None)
                    .GetAwaiter().GetResult();
                AnsiConsole.MarkupLine($"[grey]Response JSON:[/] {results.EscapeMarkup()}");
            }
            else
            {
                var results = manager.SearchFlathubAsync(
                        settings.Query,
                        page: settings.Page,
                        limit: settings.Limit,
                        ct: CancellationToken.None)
                    .GetAwaiter().GetResult();

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
        if (root.Hits is not null)
        {
            foreach (var item in root.Hits)
            {
                if (count++ >= limit) break;

                table.AddRow(
                    (item.Name ?? "Unknown").EscapeMarkup(),
                    (item.AppId ?? "Unknown").EscapeMarkup(),
                    (item.Summary ?? "").EscapeMarkup().Truncate(70)
                );
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            $"[blue]Shown:[/] {Math.Min(limit, root.Hits?.Count ?? 0)} / [blue]Total Pages:[/] {root.TotalPages} / [blue]Current Page:[/] {root.Page} / [blue]Total hits:[/] {root.TotalHits}");
    }
}
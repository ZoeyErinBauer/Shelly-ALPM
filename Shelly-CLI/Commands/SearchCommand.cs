using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class SearchSettings : DefaultSettings
{
    [CommandArgument(0, "<query>")]
    [Description("Search term to find packages")]
    public string Query { get; set; } = string.Empty;

    [CommandOption("-l|--limit <LIMIT>")]
    [Description("Maximum number of results to display (default: 25)")]
    [DefaultValue(25)]
    public int Limit { get; set; } = 25;

    [CommandOption("-i|--interactive")]
    [Description("Enable interactive mode to select and install packages")]
    public bool Interactive { get; set; }

    [CommandOption("--no-confirm")]
    [Description("Skip confirmation prompt when installing")]
    public bool NoConfirm { get; set; }
}

public class SearchCommand : Command<SearchSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] SearchSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            AnsiConsole.MarkupLine("[red]Error: Search query cannot be empty[/]");
            return 1;
        }

        try
        {
            using var manager = new AlpmManager();

            if (!settings.JsonOutput)
            {
                if (settings.Sync)
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .Start("Syncing databases and searching...", ctx => { manager.IntializeWithSync(); });
                }
                else
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .Start("Searching packages...", ctx => { manager.Initialize(); });
                }
            }
            else if (settings.Sync)
            {
                manager.IntializeWithSync();
            }
            else
            {
                manager.Initialize();
            }

            // Get all available packages and filter by query
            var allPackages = manager.GetAvailablePackages();
            var query = settings.Query.ToLowerInvariant();

            var results = allPackages
                .Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) // Exact prefix matches first
                .ThenByDescending(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))    // Name matches before description
                .ThenBy(p => p.Name)
                .Take(settings.Limit)
                .ToList();

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No packages found matching '[white]{settings.Query.EscapeMarkup()}[/]'[/]");
                return 0;
            }

            // JSON output mode
            if (settings.JsonOutput)
            {
                var json = JsonSerializer.Serialize(results, ShellyCLIJsonContext.Default.ListAlpmPackageDto);
                using var stdout = Console.OpenStandardOutput();
                using var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8);
                writer.WriteLine(json);
                writer.Flush();
                return 0;
            }

            // Display results with numbered list (paru-style)
            AnsiConsole.MarkupLine($"[blue]Found {results.Count} package(s) matching '[white]{settings.Query.EscapeMarkup()}[/]':[/]");
            AnsiConsole.WriteLine();

            for (int i = 0; i < results.Count; i++)
            {
                var pkg = results[i];
                var index = i + 1;

                // Format: [number] repo/name version
                //             Description
                AnsiConsole.MarkupLine(
                    $"[white]{index,3})[/] [magenta]{pkg.Repository.EscapeMarkup()}[/]/[green]{pkg.Name.EscapeMarkup()}[/] [blue]{pkg.Version.EscapeMarkup()}[/]");

                if (!string.IsNullOrWhiteSpace(pkg.Description))
                {
                    AnsiConsole.MarkupLine($"      [dim]{pkg.Description.EscapeMarkup().Truncate(70)}[/]");
                }
            }

            // Interactive mode - allow user to select packages to install
            if (settings.Interactive)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Enter package numbers to install (e.g., 1 2 3), or press Enter to cancel:[/]");

                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                {
                    AnsiConsole.MarkupLine("[dim]No packages selected.[/]");
                    return 0;
                }

                // Parse user selection
                var selectedPackages = new List<AlpmPackageDto>();
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int num) && num >= 1 && num <= results.Count)
                    {
                        selectedPackages.Add(results[num - 1]);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Ignoring invalid selection: {part.EscapeMarkup()}[/]");
                    }
                }

                if (selectedPackages.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No valid packages selected.[/]");
                    return 0;
                }

                var packageNames = selectedPackages.Select(p => p.Name).ToList();
                AnsiConsole.MarkupLine($"[yellow]Packages to install:[/] {string.Join(", ", packageNames)}");

                if (!settings.NoConfirm)
                {
                    if (!AnsiConsole.Confirm("Do you want to proceed?"))
                    {
                        AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                        return 0;
                    }
                }

                // Install selected packages
                manager.Progress += (sender, args) =>
                {
                    AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
                };

                manager.Question += (sender, args) =>
                {
                    if (settings.NoConfirm)
                    {
                        Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                        Console.Error.Flush();
                        var respInput = Console.ReadLine();
                        args.Response = respInput?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
                    }
                    else
                    {
                        var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                        args.Response = response ? 1 : 0;
                    }
                };

                AnsiConsole.MarkupLine("[yellow]Installing packages...[/]");
                manager.InstallPackages(packageNames);

                AnsiConsole.MarkupLine("[green]Packages installed successfully![/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Search failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}

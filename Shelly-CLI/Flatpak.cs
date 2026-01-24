using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PackageManager.Flatpak;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shelly_CLI;

public class Flatpak
{
    public sealed class FlathubSearchSettings : CommandSettings
    {
        [CommandArgument(0, "<query>")]
        [Description("Search term (passed to Flathub API as 'q')")]
        public string Query { get; init; } = string.Empty;

        [CommandOption("--limit <N>")]
        [Description("Max number of results to show")]
        [DefaultValue(21)]
        public int Limit { get; init; } = 21;

        [CommandOption("--page <N>")]
        [Description("Page number (1-based)")]
        [DefaultValue(1)]
        public int Page { get; init; } = 1;

        [CommandOption("--no-ui")]
        [Description("Returns Raw json")]
        [DefaultValue(false)]
        public bool noUi { get; init; } = false;
    }

    public class FlatpakSetting : CommandSettings
    {
        [CommandArgument(0, "<package>")]
        [Description("Package name to operate on")]
        public string Packages { get; set; } = string.Empty;
    }

    public class FlathubSearchCommand : Command<FlathubSearchSettings>
    {
        public override int Execute(CommandContext context, FlathubSearchSettings settings)
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

            foreach (var item in root.hits)
            {
                if (count++ >= limit) break;

                table.AddRow(
                    item.name.EscapeMarkup(),
                    item.app_id.EscapeMarkup(),
                    item.summary.EscapeMarkup().Truncate(70)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(
                $"[blue]Shown:[/] {Math.Min(limit, root.hits.Count)} / [blue]Total Pages:[/] {root.totalPages} / [blue]Current Page:[/] {root.page} / [blue]Total hits:[/] {root.totalHits}");
        }
    }

    public class FlatpakInstallCommand : Command<FlatpakSetting>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakSetting settings)
        {
            AnsiConsole.MarkupLine("[yellow]Installing flatpak app...[/]");
            var manager = new FlatpakManager();
            var result = manager.InstallApp(settings.Packages);

            AnsiConsole.MarkupLine("[yellow]Installed: " + result.EscapeMarkup() + "[/]");

            return 0;
        }
    }

    public class FlatpakRemoveCommand : Command<FlatpakSetting>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakSetting settings)
        {
            var manager = new FlatpakManager();
            var result = manager.UninstallApp(settings.Packages);

            AnsiConsole.MarkupLine("[yellow]" + result.EscapeMarkup() + "[/]");
            return 0;
        }
    }

    public class FlatpakUpdateCommand : Command<FlatpakSetting>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakSetting settings)
        {
            AnsiConsole.MarkupLine("[yellow]Updating flatpak app...[/]");
            var manager = new FlatpakManager();
            var result = manager.UpdateApp(settings.Packages);

            AnsiConsole.MarkupLine("[yellow]" + result.EscapeMarkup() + "[/]");

            return 0;
        }
    }

    public class FlatpakListCommand : Command
    {
        public override int Execute([NotNull] CommandContext context)
        {
            var manager = new FlatpakManager();

            var packages = manager.SearchInstalled();

            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Id");
            table.AddColumn("Version");
            table.AddColumn("Arch");
            table.AddColumn("Branch");
            table.AddColumn("Summary");

            foreach (var pkg in packages.OrderBy(p => p.Id))
            {
                table.AddRow(
                    pkg.Name,
                    pkg.Id,
                    pkg.Version,
                    pkg.Arch,
                    pkg.Version,
                    pkg.Summary.EscapeMarkup().Truncate(50)
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Total: packages[/]");
            return 0;
        }
    }
    
    public class FlatpakListUpdatesCommand : Command
    {
        public override int Execute([NotNull] CommandContext context)
        {
            var manager = new FlatpakManager();

            var packages = manager.GetPackagesWithUpdates();

            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Id");
            table.AddColumn("Version");

            foreach (var pkg in packages.OrderBy(p => p.Id))
            {
                table.AddRow(
                    pkg.Name,
                    pkg.Id,
                    pkg.Version
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[blue]Total: packages[/]");
            return 0;
        }
    }

    public class FlatpakRunCommand : Command<FlatpakSetting>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakSetting settings)
        {
            AnsiConsole.MarkupLine("[yellow]Running selected flatpak app...[/]");
            var result = new FlatpakManager().LaunchApp(settings.Packages);

            //AnsiConsole.MarkupLine("Result:" +result);
            if (result)
            {
                AnsiConsole.MarkupLine("[green]App launched successfully[/]");
                return 0;
           }

            AnsiConsole.MarkupLine("[red]Failed to launch app[/]");
            return 1;
        }
    }

    public class FlatpakRunningCommand : Command
    {
        public override int Execute([NotNull] CommandContext context)
        {
            AnsiConsole.MarkupLine("[yellow]Currently running flatpack instances on machine...[/]");
            var result = new FlatpakManager().GetRunningInstances();

            if (result.Count > 0)
            {
                var table = new Table();
                table.AddColumn("Id");
                table.AddColumn("Pid");

                foreach (var pkg in result.OrderBy(pkg => pkg.Pid))
                {
                    table.AddRow(
                        pkg.AppId,
                        pkg.Pid.ToString()
                    );
                }

                AnsiConsole.Write(table);
                return 0;
            }

            AnsiConsole.MarkupLine("[green]No instances running[/]");
            return 0;
        }
    }

    public class FlatpakKillCommand : Command<FlatpakSetting>
    {
        public override int Execute([NotNull] CommandContext context, [NotNull] FlatpakSetting settings)
        {
            AnsiConsole.MarkupLine("[yellow]Killing selected flatpak app...[/]");
            var result = new FlatpakManager().KillApp(settings.Packages);

            AnsiConsole.MarkupLine("[red]" + result + "[/]");


            return 0;
        }
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands;

public class VersionCommand : Command
{
    public override int Execute([NotNull] CommandContext context)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        AnsiConsole.MarkupLine($"[bold]shelly[/] version [green]{version}[/]");
        return 0;
    }
}

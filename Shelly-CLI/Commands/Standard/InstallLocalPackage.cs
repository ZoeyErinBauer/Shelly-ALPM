using PackageManager.Alpm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class InstallLocalPackage : Command<InstallLocalPackageSettings>
{
    public override int Execute(CommandContext context, InstallLocalPackageSettings settings)
    {
        //Validate the file location and that a file is actually passed in
        if (settings.PackageLocation == null)
        {
            AnsiConsole.MarkupLine("[red]Error: No package specified[/]");
            return 1;
        }

        if (!File.Exists(settings.PackageLocation))
        {
            AnsiConsole.MarkupLine("[red]Error: Specified file does not exist.[/]");
            return 1;
        }

        var manager = new AlpmManager();
        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            // Handle SelectProvider differently - it needs a selection, not yes/no
            if (args.QuestionType == AlpmQuestionType.SelectProvider && args.ProviderOptions?.Count > 0)
            {
                if (settings.NoConfirm)
                {
                    // Machine-readable format for UI integration
                    Console.Error.WriteLine($"[ALPM_SELECT_PROVIDER]{args.DependencyName}");
                    for (int i = 0; i < args.ProviderOptions.Count; i++)
                    {
                        Console.Error.WriteLine($"[ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                    }

                    Console.Error.WriteLine("[ALPM_PROVIDER_OPTION_END]");
                    Console.Error.Flush();
                    var input = Console.ReadLine();
                    args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
                }
                else
                {
                    var selection = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"[yellow]{args.QuestionText}[/]")
                            .AddChoices(args.ProviderOptions));
                    args.Response = args.ProviderOptions.IndexOf(selection);
                }
            }
            else if (settings.NoConfirm)
            {
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
            }
        };

        AnsiConsole.MarkupLine("[yellow]Initializing and syncing ALPM...[/]");
        manager.Initialize();
        manager.InstallLocalPackage(Path.GetFullPath(settings.PackageLocation));
        return 0;
    }

    private bool IsArchPackage(string directoryPath)
    {
        return File.Exists(Path.Combine(directoryPath, "PKGBUILD"));
    }
}
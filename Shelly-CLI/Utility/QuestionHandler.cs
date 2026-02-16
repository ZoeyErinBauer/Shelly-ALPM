using PackageManager.Alpm;
using Spectre.Console;

namespace Shelly_CLI.Utility;

public class QuestionHandler
{
    public static void HandleConflict(AlpmQuestionEventArgs question, bool uiMode = false, bool noConfirm = false)
    {
        if (question.ProviderOptions is null)
            throw new ArgumentNullException(nameof(question.ProviderOptions),
                "Cannot have a conflict while provider option is null!");
        if (uiMode)
        {
            if (noConfirm)
            {
                // Returns default response
                question.Response = 1;
                return;
            }

            Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT]{question.QuestionText}");
            for (var i = 0; i < question.ProviderOptions.Count; i++)
            {
                Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT_OPTION]{i}:{question.ProviderOptions[i]}");
            }

            Console.Error.WriteLine("[Shelly][ALPM_CONFLICT_END]");
            Console.Error.Flush();
            var input = Console.ReadLine();
            question.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
            return;
        }

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[yellow]{question.QuestionText}[/]")
                .AddChoices(question.ProviderOptions!));
        question.Response = question.ProviderOptions!.IndexOf(selection);
    }
}
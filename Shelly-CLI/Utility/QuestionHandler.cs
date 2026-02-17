using PackageManager.Alpm;
using Spectre.Console;

namespace Shelly_CLI.Utility;

public static class QuestionHandler
{
    public static void HandleQuestion(AlpmQuestionEventArgs question, bool uiMode = false, bool noConfirm = false)
    {
        switch (question.QuestionType)
        {
            case AlpmQuestionType.SelectProvider:
                HandleProviderSelection(question, uiMode, noConfirm);
                break;
            case AlpmQuestionType.ReplacePkg:
                
            case AlpmQuestionType.ConflictPkg:
            case AlpmQuestionType.InstallIgnorePkg:
            case AlpmQuestionType.CorruptedPkg:
            case AlpmQuestionType.ImportKey:
            default:
                HandleYesNoQuestion(question, uiMode, noConfirm);
                break;
        }
    }
    
    private static void HandleProviderSelection(AlpmQuestionEventArgs question, bool uiMode = false,
        bool noConfirm = false)
    {
        if (question.ProviderOptions is null)
            throw new ArgumentNullException(nameof(question.ProviderOptions),
                "Cannot have a selection while provider options is null!");
        if (uiMode)
        {
            if (noConfirm)
            {
                question.SetResponse(0);
                return;
            }

            Console.Error.WriteLine($"[ALPM_SELECT_PROVIDER]{question.DependencyName}");
            for (int i = 0; i < question.ProviderOptions.Count; i++)
            {
                Console.Error.WriteLine($"[ALPM_PROVIDER_OPTION]{i}:{question.ProviderOptions[i]}");
            }

            Console.Error.WriteLine("[ALPM_PROVIDER_END]");
            Console.Error.Flush();
            var input = Console.ReadLine();
            question.SetResponse(int.TryParse(input?.Trim(), out var idx) ? idx : 0);
            return;
        }

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[yellow]{question.QuestionText}[/]")
                .AddChoices(question.ProviderOptions!));
        question.SetResponse(question.ProviderOptions!.IndexOf(selection));
    }


    private static void HandleYesNoQuestion(AlpmQuestionEventArgs question, bool uiMode = false,
        bool noConfirm = false)
    {
        if (uiMode)
        {
            if (noConfirm)
            {
                question.SetResponse(1);
                return;
            }

            Console.Error.WriteLine($"[ALPM_QUESTION]{question.QuestionText}");
            Console.Error.Flush();
            var input = Console.ReadLine();
            question.SetResponse(int.TryParse(input, out var result) ? result : 0);

            return;
        }

        if (noConfirm)
        {
            question.SetResponse(1);
            return;
        }

        var response = AnsiConsole.Confirm($"[yellow]{question.QuestionText}[/]", defaultValue: true);
        question.SetResponse(response ? 1 : 0);
    }
}
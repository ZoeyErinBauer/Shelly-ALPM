using PackageManager.Alpm;

namespace Shelly;

public class QuestionHandler
{
    public static void HandleReplacePkg(AlpmReplacesEventArgs replaceArgs, bool uiMode = false, bool noConfirm = false)
    {
        if (uiMode)
        {
            foreach (var replace in replaceArgs.Replaces)
            {
                if (noConfirm)
                {
                    Console.Error.WriteLine(
                        $"Replacement: {replaceArgs.Repository}/{replaceArgs.PackageName} replaces {replace}");
                    continue;
                }

                Console.Error.WriteLine(
                    $"[ALPM_QUESTION_REPLACEPKG]{replaceArgs.Repository}/{replaceArgs.PackageName} replaces {replace}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                Console.WriteLine($"Received: {input}");
            }
            return;
        }

        if (noConfirm)
        {
            foreach (var replace in replaceArgs.Replaces)
            {
                Console.WriteLine(
                    $"Replacement: {replaceArgs.Repository}/{replaceArgs.PackageName} replaces {replace}");
            }
            return;
        }

        foreach (var replace in replaceArgs.Replaces)
        {
            var response = ShowConfirmPrompt(
                $"Replace {replace} with {replaceArgs.Repository}/{replaceArgs.PackageName}?",
                defaultValue: true);
            if (!response)
            {
                Console.WriteLine($"Skipping replacement of {replace}");
            }
        }
    }
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
    
    /// <summary>
    /// Displays an interactive selection prompt in the terminal.
    /// Navigate with Up/Down arrow keys or type a number to jump directly.
    /// Press Enter to confirm.
    /// </summary>
    private static int ShowSelectionPrompt(string title, IList<string> choices)
    {
        int selected = 0;
        bool done = false;
        string numberBuffer = "";

        Console.CursorVisible = false;
        Console.WriteLine(title);
        int startRow = Console.CursorTop;

        void Render()
        {
            Console.SetCursorPosition(0, startRow);
            for (int i = 0; i < choices.Count; i++)
            {
                if (i == selected)
                {
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.BackgroundColor = ConsoleColor.Cyan;
                    Console.Write($" > {i + 1}) {choices[i]}");
                    Console.ResetColor();
                    // Clear rest of line
                    Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft)));
                    Console.WriteLine();
                }
                else
                {
                    Console.Write($"   {i + 1}) {choices[i]}");
                    Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft)));
                    Console.WriteLine();
                }
            }

            if (numberBuffer.Length > 0)
                Console.Write($"  # {numberBuffer}");
            else
                Console.Write(new string(' ', 20));

            Console.SetCursorPosition(0, startRow + choices.Count);
        }

        Render();

        while (!done)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    numberBuffer = "";
                    selected = (selected - 1 + choices.Count) % choices.Count;
                    break;
                case ConsoleKey.DownArrow:
                    numberBuffer = "";
                    selected = (selected + 1) % choices.Count;
                    break;
                case ConsoleKey.Enter:
                    done = true;
                    break;
                case ConsoleKey.Backspace:
                    if (numberBuffer.Length > 0)
                        numberBuffer = numberBuffer[..^1];
                    break;
                default:
                    if (char.IsDigit(key.KeyChar))
                    {
                        numberBuffer += key.KeyChar;
                        if (int.TryParse(numberBuffer, out int num) && num >= 1 && num <= choices.Count)
                        {
                            selected = num - 1;
                        }
                    }
                    break;
            }

            Render();
        }

        Console.CursorVisible = true;
        Console.WriteLine();
        return selected;
    }

    /// <summary>
    /// Displays an interactive Yes/No confirmation prompt.
    /// Navigate with Left/Right or Up/Down arrow keys, or type y/n.
    /// Press Enter to confirm.
    /// </summary>
    private static bool ShowConfirmPrompt(string title, bool defaultValue = true)
    {
        bool value = defaultValue;
        Console.CursorVisible = false;
        Console.Write($"{title} ");

        void Render()
        {
            int left = Console.CursorLeft;
            if (value)
            {
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = ConsoleColor.Green;
                Console.Write(" Yes ");
                Console.ResetColor();
                Console.Write(" / No ");
            }
            else
            {
                Console.Write(" Yes / ");
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = ConsoleColor.Red;
                Console.Write(" No ");
                Console.ResetColor();
            }

            Console.Write("  ");
        }

        int lineStart = Console.CursorLeft;
        Render();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                case ConsoleKey.UpArrow:
                    value = true;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.DownArrow:
                    value = false;
                    break;
                case ConsoleKey.Enter:
                    Console.CursorVisible = true;
                    Console.WriteLine();
                    return value;
                default:
                    if (key.KeyChar is 'y' or 'Y')
                        value = true;
                    else if (key.KeyChar is 'n' or 'N')
                        value = false;
                    break;
            }

            Console.CursorLeft = lineStart;
            Render();
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
            if (int.TryParse(input?.Trim(), out var idx))
            {
                question.SetResponse(idx);
            }
            return;
        }

        if (noConfirm)
        {
            question.SetResponse(0);
            return;
        }

        var selectedIndex = ShowSelectionPrompt(question.QuestionText ?? "Select a provider:", question.ProviderOptions!);
        question.SetResponse(selectedIndex);
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

            switch (question.QuestionType)
            {
                case AlpmQuestionType.ConflictPkg:
                    Console.Error.WriteLine($"[ALPM_QUESTION_CONFLICT]{question.QuestionText}");
                    break;
                case AlpmQuestionType.ReplacePkg:
                    Console.Error.WriteLine($"[ALPM_QUESTION_REPLACEPKG]{question.QuestionText}");
                    break;
                case AlpmQuestionType.CorruptedPkg:
                    Console.Error.WriteLine($"[ALPM_QUESTION_CORRUPTEDPKG]{question.QuestionText}");
                    break;
                case AlpmQuestionType.ImportKey:
                    Console.Error.WriteLine($"[ALPM_QUESTION_IMPORTKEY]{question.QuestionText}");
                    break;
                case AlpmQuestionType.SelectProvider:
                    throw new Exception("Select provider is never a y / n question and is being invoked as one.");
                case AlpmQuestionType.RemovePkgs:
                    Console.Error.WriteLine($"[ALPM_QUESTION_REMOVEPKG]{question.QuestionText}");
                    break;
                case AlpmQuestionType.InstallIgnorePkg:
                default:
                    Console.Error.WriteLine($"[ALPM_QUESTION]{question.QuestionText}");
                    break;
            }

            Console.Error.Flush();
            var input = Console.ReadLine();
            Console.WriteLine($"Received: {input}");
            if (input is "y" or "Y")
            {
                question.SetResponse(1);
            }
            else if (input is "n" or "N")
            {
                question.SetResponse(0);
            }
            return;
        }

        if (noConfirm)
        {
            question.SetResponse(1);
            return;
        }

        var response = ShowConfirmPrompt(question.QuestionText ?? "Confirm?", defaultValue: true);
        question.SetResponse(response ? 1 : 0);
    }
}
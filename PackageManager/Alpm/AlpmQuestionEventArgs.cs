using System;
using System.Collections.Generic;

namespace PackageManager.Alpm;

public class AlpmQuestionEventArgs : EventArgs
{
    public AlpmQuestionEventArgs(
        AlpmQuestionType questionType,
        string questionText,
        List<string>? providerOptions = null,
        string? dependencyName = null)
    {
        QuestionType = questionType;
        QuestionText = questionText;
        ProviderOptions = providerOptions;
        DependencyName = dependencyName;
    }

    /// <summary>
    /// The type of question being asked by libalpm
    /// </summary>
    public AlpmQuestionType QuestionType { get; }
    
    /// <summary>
    /// The question text to display to the user
    /// </summary>
    public string QuestionText { get; }
    
    /// <summary>
    /// For SelectProvider questions: the list of package names that can provide the dependency
    /// </summary>
    public List<string>? ProviderOptions { get; }
    
    /// <summary>
    /// For SelectProvider questions: the name of the dependency being resolved
    /// </summary>
    public string? DependencyName { get; }
    
    /// <summary>
    /// The response to send back to libalpm.
    /// For yes/no questions: 1 = Yes, 0 = No
    /// For SelectProvider: the index of the selected provider (0-based)
    /// </summary>
    public int Response { get; set; } = 0; // Default to No (0)
}

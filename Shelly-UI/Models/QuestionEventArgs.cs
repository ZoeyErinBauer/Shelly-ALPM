using System;
using System.Collections.Generic;
using System.Threading;

namespace Shelly_UI.Models;

public class QuestionEventArgs : EventArgs
{
    public QuestionEventArgs(
        QuestionType questionType,
        string questionText,
        List<string>? providerOptions = null,
        string? dependencyName = null)
    {
        QuestionType = questionType;
        QuestionText = questionText;
        ProviderOptions = providerOptions;
        DependencyName = dependencyName;
    }

    public QuestionType QuestionType { get; }
    public string QuestionText { get; }
    public List<string>? ProviderOptions { get; }
    public string? DependencyName { get; }
    public int Response { get; set; } = 0;

    private volatile bool _responded;

    public void SetResponse(int response)
    {
        Response = response;
        _responded = true;
    }

    public void WaitForResponse()
    {
        while (!_responded)
        {
            Thread.Sleep(50);
        }
    }
}

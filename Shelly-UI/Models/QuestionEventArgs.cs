using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shelly_UI.Models;

public class QuestionEventArgs : EventArgs
{
    private readonly TaskCompletionSource<int> _tcs = new();
    public Task<int> ResponseTask => _tcs.Task;

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
    public int Response { get; private set; } = -1;

    public void SetResponse(int response)
    {
        Response = response;
        _tcs.TrySetResult(response);
    }

    public Task WaitForResponseAsync()
    {
        return _tcs.Task;
    }
}

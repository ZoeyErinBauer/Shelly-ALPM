using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Concurrency;
using System.Text;
using ReactiveUI;

namespace Shelly_UI.Services;

public class ConsoleLogService : TextWriter
{
    private static readonly Lazy<ConsoleLogService> _instance = new(() => new ConsoleLogService());
    public static ConsoleLogService Instance => _instance.Value;

    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private StringBuilder _lineBuffer = new();
    public ObservableCollection<string> Logs { get; } = new();

    private ConsoleLogService()
    {
        _originalOut = Console.Out;
        Console.SetOut(this);
        Console.SetError(this);
        
        // This log proves the redirection is active
        this.WriteLine("Logging Service Active.");
    }

    public override void WriteLine(string? value)
    {
        if (value != null)
        {
            // Use Task.Run if Schedule is failing silently in AOT
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Logs.Add($"[{DateTime.Now:HH:mm:ss}] {value}");
                if (Logs.Count > 500) Logs.RemoveAt(0);
            });
        }
        _originalOut.WriteLine(value);
    }
    
    public void LogError(string message)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}");
            if (Logs.Count > 500) Logs.RemoveAt(0);
        });
        _originalError.WriteLine(message);
    }

    // Overriding this ensures that objects passed to Console.WriteLine are caught
    public override void WriteLine(object? value) => WriteLine(value?.ToString());

    // Basic Write to catch partials without complex buffering for now
    public override void Write(string? value) 
    {
        _originalOut.Write(value);
        if (value != null && value.EndsWith(Environment.NewLine))
        {
            WriteLine(value.TrimEnd());
        }
    }

    public override Encoding Encoding => Encoding.UTF8;
}
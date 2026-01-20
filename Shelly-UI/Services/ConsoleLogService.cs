using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Concurrency;
using System.Text;
using System.Text.RegularExpressions;
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
    
    private readonly Dictionary<string, int> _packageProgress = new();
    
    // Matches patterns like "Downloading packageName... (50%)" or "Processing packageName... (75%)"
    private static readonly Regex PercentagePattern = new(@"^(?:Progress|Processing)\s+(.+?)\.\.\.\s+\((\d+)%\)$", RegexOptions.Compiled);

    private ConsoleLogService()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        
        Console.SetError(this);
    }

    private const string ShellyPrefix = "[Shelly]";
    
    public override void WriteLine(string? value)
    {
        if (value != null)
        {
            // Only process logs that start with [Shelly] prefix
            if (value.StartsWith(ShellyPrefix))
            {
                // Remove the prefix for display
                var logMessage = value.Substring(ShellyPrefix.Length);
                
                // Check if the message contains a percentage pattern and update package progress
                var match = PercentagePattern.Match(logMessage);
                if (match.Success)
                {
                    var packageName = match.Groups[1].Value;
                    if (int.TryParse(match.Groups[2].Value, out var percent))
                    {
                        UpdatePackageProgress(packageName, percent);
                        return; // UpdatePackageProgress will call WriteLine with the progress change message
                    }
                }
                
                AddLog(logMessage);
            }
        }
        _originalError.WriteLine(value);
    }
    
    public override void WriteLine(object? value) => WriteLine(value?.ToString());
    
    public override void Write(string? value) 
    {
        _originalError.Write(value);
    }

    public override Encoding Encoding => Encoding.UTF8;
    
    private void AddLog(string message)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (Logs.Count > 500) Logs.RemoveAt(0);
        });
    }
    
    public void UpdatePackageProgress(string? packageName, int? percent)
    {
        if (string.IsNullOrEmpty(packageName) || !percent.HasValue)
            return;
            
        var currentPercent = percent.Value;
        
        if (_packageProgress.TryGetValue(packageName, out var previousPercent))
        {
            if (currentPercent != previousPercent)
            {
                _packageProgress[packageName] = currentPercent;
                AddLog($"{packageName}: {previousPercent}% -> {currentPercent}%");
            }
        }
        else
        {
            _packageProgress[packageName] = currentPercent;
            AddLog($"{packageName}: 0% -> {currentPercent}%");
        }
    }
    
    public void ClearPackageProgress()
    {
        _packageProgress.Clear();
    }
}
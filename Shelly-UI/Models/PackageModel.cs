using System;
using ReactiveUI;

namespace Shelly_UI.Models;

public class PackageModel : ReactiveObject
{
    public required string Name { get; set; }

    public required string Version { get; set; }

    public required long DownloadSize { get; set; }

    // Helper property to format bytes to MB
    public string SizeString => $"{(DownloadSize / 1024.0 / 1024.0):F2} MB";
    
    public long InstallSize { get; set; }

    public string? Description { get; set; }

    public string? Url { get; set; }

    public string? Repository { get; set; }

    public bool IsInstalled { get; set; } = false;
    
    public string InstallDate { get; set; } = string.Empty;

    private bool _isChecked;

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}
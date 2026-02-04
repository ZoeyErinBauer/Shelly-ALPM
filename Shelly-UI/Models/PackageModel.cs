using System.Collections.ObjectModel;
using ReactiveUI;

namespace Shelly_UI.Models;

public class PackageModel : ReactiveObject
{
    public required string Name { get; set; }

    public required string Version { get; set; }

    public required long DownloadSize { get; set; }

    // Helper property to format bytes to MB
    public string SizeString => $"{(DownloadSize / 1024.0 / 1024.0):F2} MB";

    public string? Description { get; set; }

    public string? Url { get; set; }

    public string? Repository { get; set; }

    public bool IsInstalled { get; set; } = false;

    private bool _isChecked;
    
    
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
    
    private ObservableCollection<PackageModel>? _children;
    public ObservableCollection<PackageModel>? Children
    {
        get => _children;
        set => this.RaiseAndSetIfChanged(ref _children, value);
    }
    
    public bool HasChildren => Children?.Count > 0;

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}
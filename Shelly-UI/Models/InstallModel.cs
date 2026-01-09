using ReactiveUI;

namespace Shelly_UI.Models;

public class InstallModel : ReactiveObject
{
    public required string Name { get; set; }
    
    public required string Version  { get; set; }
   
    public required long DownloadSize { get; set; }
    
    // Helper property to format bytes to MB
    public string SizeString => $"{(DownloadSize / 1024.0 / 1024.0):F2} MB";

    private bool _isChecked;
    public bool IsChecked { 
        get => _isChecked; 
        set => this.RaiseAndSetIfChanged(ref _isChecked, value); 
    }
}
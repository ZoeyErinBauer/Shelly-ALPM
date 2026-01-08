using System;
using System.Collections.ObjectModel;
using PackageManager.Alpm;
using ReactiveUI;

namespace Shelly_UI.ViewModels;

public class UpdateViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }

    public UpdateViewModel(IScreen screen)
    {
        HostScreen = screen;
        PackagesForUpdating = new ObservableCollection<AlpmPackageUpdate>(new AlpmManager().GetPackagesNeedingUpdate());
      
    }
    
    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
    
    public ObservableCollection<AlpmPackageUpdate> PackagesForUpdating { get; set; }
}
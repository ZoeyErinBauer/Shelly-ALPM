using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;

namespace Shelly_UI.BaseClasses;

public abstract class ConsoleEnabledViewModelBase : ReactiveObject
{
    // Expose the collection directly for the custom control
    public System.Collections.ObjectModel.ObservableCollection<string> Logs => 
        ConsoleLogService.Instance.Logs;


    private bool _isBottomPanelCollapsed = true;
    public bool IsBottomPanelCollapsed
    {
        get => _isBottomPanelCollapsed;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelCollapsed, value);
    }

    private bool _isBottomPanelVisible;
    public bool IsBottomPanelVisible
    {
        get => _isBottomPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelVisible, value);
    }

    protected ConsoleEnabledViewModelBase()
    {
        var consoleEnabled =  App.Services.GetRequiredService<IConfigService>().LoadConfig().ConsoleEnabled;
        _isBottomPanelVisible = consoleEnabled;
    }

    public void ToggleBottomPanel()
    {
        IsBottomPanelCollapsed = !IsBottomPanelCollapsed;
    }
}

using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.Messages;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;

namespace Shelly_UI.BaseClasses;

public abstract class ConsoleEnabledViewModelBase : ReactiveObject, IDisposable
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

    private readonly CompositeDisposable _disposables = new CompositeDisposable();
    protected CompositeDisposable Disposables => _disposables;

    protected ConsoleEnabledViewModelBase()
    {
        var consoleEnabled =  App.Services.GetRequiredService<IConfigService>().LoadConfig().ConsoleEnabled;
        _isBottomPanelVisible = consoleEnabled;

        MessageBus.Current.Listen<ConsoleEnableMessage>()
            .Subscribe(RefreshUi);
        //will be disposed by message bus in main
    }

    private void RefreshUi(ConsoleEnableMessage msg)
    {
        if (IsBottomPanelCollapsed)
        {
            IsBottomPanelCollapsed = false;
        }
        ToggleBottomPanel();
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }

    public void ToggleBottomPanel()
    {
        IsBottomPanelCollapsed = !IsBottomPanelCollapsed;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposables?.Dispose();
        }
    }
}

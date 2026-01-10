using System.Reactive.Linq;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels;
using System;
using System.Reactive.Disposables;

namespace Shelly_UI.Views;

public partial class PasswordWindow : ReactiveWindow<PasswordViewModel>
{
    public PasswordWindow()
    {
        InitializeComponent();
        this.WhenActivated((CompositeDisposable disposables) =>
        {
            ViewModel!.ConfirmCommand.Subscribe(password => Close(password)).DisposeWith(disposables);
            ViewModel!.CancelCommand.Subscribe(_ => Close(null)).DisposeWith(disposables);
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

public static class DisposableExtensions
{
    public static T DisposeWith<T>(this T item, CompositeDisposable compositeDisposable) where T : IDisposable
    {
        compositeDisposable.Add(item);
        return item;
    }
}

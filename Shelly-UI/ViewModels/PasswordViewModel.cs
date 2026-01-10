using System.Reactive;
using ReactiveUI;

namespace Shelly_UI.ViewModels;

public class PasswordViewModel : ViewModelBase
{
    private string _password = string.Empty;

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public ReactiveCommand<Unit, string> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public PasswordViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(() => Password);
        CancelCommand = ReactiveCommand.Create(() => { });
    }
}

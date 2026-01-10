namespace Shelly_UI.Services;

public interface IPasswordService
{
    string? GetPassword();
    void SetPassword(string password);
    bool HasPassword();
}

public class PasswordService : IPasswordService
{
    private string? _password;

    public string? GetPassword() => _password;

    public void SetPassword(string password)
    {
        _password = password;
    }

    public bool HasPassword() => !string.IsNullOrEmpty(_password);
}

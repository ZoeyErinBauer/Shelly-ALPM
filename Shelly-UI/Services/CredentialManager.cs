using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Shelly_UI.Services;

public class CredentialManager : ICredentialManager
{
    private string? _storedPassword;
    private bool _isValidated;
    private TaskCompletionSource<bool>? _pendingRequest;
    private readonly object _lock = new();

    public bool HasStoredCredentials
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrEmpty(_storedPassword);
            }
        }
    }

    public bool IsValidated
    {
        get
        {
            lock (_lock)
            {
                return _isValidated;
            }
        }
    }

    public void StorePassword(string password)
    {
        lock (_lock)
        {
            _storedPassword = password;
            _isValidated = false;
        }
    }

    public string? GetPassword()
    {
        lock (_lock)
        {
            return _storedPassword;
        }
    }

    public void ClearCredentials()
    {
        lock (_lock)
        {
            // Overwrite the password in memory before clearing
            if (_storedPassword != null)
            {
                var length = _storedPassword.Length;
                _storedPassword = new string('\0', length);
            }
            _storedPassword = null;
            _isValidated = false;
        }
    }

    public void MarkAsValidated()
    {
        lock (_lock)
        {
            _isValidated = true;
        }
    }

    public void MarkAsInvalid()
    {
        lock (_lock)
        {
            _isValidated = false;
            // Clear invalid credentials
            if (_storedPassword != null)
            {
                var length = _storedPassword.Length;
                _storedPassword = new string('\0', length);
            }
            _storedPassword = null;
        }
    }

    public event EventHandler<CredentialRequestEventArgs>? CredentialRequested;

    public async Task<bool> RequestCredentialsAsync(string reason)
    {
        TaskCompletionSource<bool> tcs;
        bool shouldRaiseEvent = false;

        lock (_lock)
        {
            // If we already have validated credentials, return immediately
            if (HasStoredCredentials)
            {
                return true;
            }

            // If there's already a pending request, return its task
            if (_pendingRequest != null)
            {
                tcs = _pendingRequest;
            }
            else
            {
                // Create a new completion source for this request
                _pendingRequest = new TaskCompletionSource<bool>();
                tcs = _pendingRequest;
                shouldRaiseEvent = true;
            }
        }

        if (shouldRaiseEvent)
        {
            // Raise the event to request credentials from the UI
            CredentialRequested?.Invoke(this, new CredentialRequestEventArgs(reason));
        }

        // Wait for the UI to complete the request
        return await tcs.Task;
    }

    public async Task CompleteCredentialRequestAsync(bool success)
    {
        TaskCompletionSource<bool>? tcs;

        lock (_lock)
        {
            tcs = _pendingRequest;
        }

        if (success)
        {
            var isValid = await ValidateInputCredentials();
            if (isValid)
            {
                lock (_lock)
                {
                    _pendingRequest = null;
                }
                tcs?.TrySetResult(true);
            }
        }
        else
        {
            lock (_lock)
            {
                _pendingRequest = null;
            }
            tcs?.TrySetResult(false);
        }
    }

    public async Task<bool> ValidateInputCredentials()
    {
        var password = GetPassword();
        var username = Environment.UserName;

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "su",
            Arguments = $"-c true {username}",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            process.Start();

            // Pipe the password to su
            await process.StandardInput.WriteLineAsync(password);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("Credentials verified successfully via su.");
                MarkAsValidated();
                return true;
            }
            else
            {
                Console.Error.WriteLine("Authentication failed via su. Clearing credentials.");
                MarkAsInvalid();
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during authentication check: {ex.Message}");
        }

        return false;
    }
}
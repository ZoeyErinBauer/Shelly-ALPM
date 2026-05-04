namespace Shelly.Gtk.Services;

public interface IDirtyService
{
    /// <summary>Raised when something has changed and UIs should reload.</summary>
    event EventHandler<DirtyEventArgs>? Dirtied;

    /// <summary>True if any of the given scopes (or <see cref="DirtyScopes.All"/>) are currently dirty.</summary>
    bool IsDirty(params string[] scopes);

    /// <summary>Mark a scope dirty and notify listeners.</summary>
    void MarkDirty(string scope = DirtyScopes.All);

    /// <summary>Clear the dirty flag(s). Pass no scopes (or <see cref="DirtyScopes.All"/>) to clear all.</summary>
    void Clear(params string[] scopes);
}

public sealed class DirtyEventArgs(string scope) : EventArgs
{
    public string Scope { get; } = scope;

    public bool Matches(params string[] listeningScopes)
        => DirtyScopes.Matches(Scope, listeningScopes);
}

using System.Collections.Concurrent;

namespace Shelly.Gtk.Services;

public class DirtyService : IDirtyService
{
    private readonly ConcurrentDictionary<string, byte> _dirty = new();

    public event EventHandler<DirtyEventArgs>? Dirtied;

    public bool IsDirty(params string[] scopes)
    {
        if (_dirty.ContainsKey(DirtyScopes.All)) return true;
        if (scopes.Length == 0) return !_dirty.IsEmpty;
        foreach (var key in _dirty.Keys)
            if (DirtyScopes.Matches(key, scopes)) return true;
        return false;
    }

    public void MarkDirty(string scope = DirtyScopes.All)
    {
        _dirty[scope] = 0;
        Dirtied?.Invoke(this, new DirtyEventArgs(scope));
    }

    public void Clear(params string[] scopes)
    {
        if (scopes.Length == 0 || Array.IndexOf(scopes, DirtyScopes.All) >= 0)
        {
            _dirty.Clear();
            return;
        }
        foreach (var key in _dirty.Keys.ToArray())
            if (DirtyScopes.Matches(key, scopes))
                _dirty.TryRemove(key, out _);
    }
}

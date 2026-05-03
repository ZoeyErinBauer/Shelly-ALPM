using Shelly.Gtk.Services;

namespace Shelly.Gtk.Windows;

/// <summary>
/// Implemented by windows that can be reloaded in response to <see cref="IDirtyService"/> events.
/// </summary>
public interface IReloadable
{
    /// <summary>Scopes this window listens to.</summary>
    string[] ListensTo { get; }

    /// <summary>Reload the window's data/UI. Always invoked on the GTK main thread.</summary>
    void Reload();
}

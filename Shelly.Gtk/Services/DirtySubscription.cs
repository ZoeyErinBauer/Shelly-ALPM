using Shelly.Gtk.Windows;

namespace Shelly.Gtk.Services;

/// <summary>
/// Reusable wiring helper that subscribes an <see cref="IReloadable"/> window to
/// <see cref="IDirtyService"/> events. It filters events by the window's listening
/// scopes, marshals reload to the GTK main thread, and clears the dirty flag(s)
/// after a successful reload. Disposal removes the handler — call from window Dispose.
/// </summary>
public sealed class DirtySubscription : IDisposable
{
    private readonly IDirtyService _dirty;
    private readonly IReloadable _target;
    private bool _disposed;
    private bool _scheduled;
    private int _reloading;

    private DirtySubscription(IDirtyService dirty, IReloadable target)
    {
        _dirty = dirty;
        _target = target;
        _dirty.Dirtied += OnDirtied;

        // If something was marked dirty before we subscribed, schedule a reload now.
        if (_dirty.IsDirty(target.ListensTo))
            ScheduleReload();
    }

    public static DirtySubscription Attach(IDirtyService dirty, IReloadable target)
        => new(dirty, target);

    private void OnDirtied(object? sender, DirtyEventArgs e)
    {
        if (!e.Matches(_target.ListensTo)) return;
        ScheduleReload();
    }

    private void ScheduleReload()
    {
        // Coalesce: if a reload is already pending on the idle loop, drop this request.
        // Multiple scopes (e.g. Native + NativeInstalled) firing in the same tick
        // collapse into a single Reload() call.
        lock (this)
        {
            if (_scheduled || _disposed) return;
            _scheduled = true;
        }

        GLib.Functions.IdleAdd(0, () =>
        {
            lock (this) { _scheduled = false; }
            if (_disposed) return false;

            // Guard against re-entrant reloads (e.g. Reload() itself causes a MarkDirty
            // before returning). Only one Reload runs at a time per subscription.
            if (Interlocked.Exchange(ref _reloading, 1) == 1) return false;
            try { _target.Reload(); }
            catch (Exception ex) { Console.Error.WriteLine($"DirtySubscription reload failed: {ex.Message}"); }
            finally
            {
                _dirty.Clear(_target.ListensTo);
                Interlocked.Exchange(ref _reloading, 0);
            }
            return false; // run once
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dirty.Dirtied -= OnDirtied;
    }
}

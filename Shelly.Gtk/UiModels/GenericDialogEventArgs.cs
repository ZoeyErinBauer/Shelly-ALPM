using Gtk;

namespace Shelly.Gtk.UiModels;

public class GenericDialogEventArgs(Box box)
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    public virtual Task ResponseTask => _tcs.Task;

    public Box Box { get; } = box;

    public virtual void SetResponse(bool response)
    {
        _tcs.TrySetResult(response);
    }
}

public class GenericDialogEventArgs<TResult>(Box box) : GenericDialogEventArgs(box)
{
    private readonly TaskCompletionSource<TResult> _tcs = new();
    public override Task<TResult> ResponseTask => _tcs.Task;

    public void SetResponse(TResult response)
    {
        _tcs.TrySetResult(response);
    }

    public override void SetResponse(bool response)
    {
        if (!response && typeof(TResult) == typeof(bool))
        {
            _tcs.TrySetResult((TResult)(object)false);
        }
        
        base.SetResponse(response);
    }
}
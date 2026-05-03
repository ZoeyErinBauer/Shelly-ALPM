namespace Shelly.Gtk.UiModels;

public class PackageBuildEventArgs(string title, string pkgBuild) : EventArgs
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    public Task<bool> ResponseTask => _tcs.Task;

    public string Title { get; } = title;
    public string PkgBuild { get; } = pkgBuild;

    public void SetResponse(bool response)
    {
        _tcs.TrySetResult(response);
    }
}
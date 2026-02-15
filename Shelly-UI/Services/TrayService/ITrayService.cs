using System;
using System.Threading.Tasks;

namespace Shelly_UI.Services.TrayService;

public interface ITrayService : IDisposable
{
    public void Start();

    internal Task CheckForUpdates();

    public void Stop();
}
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;

public interface IOperationLogService
{
    Task<List<OperationLogEntry>> GetRecentOperationsAsync(int count = 10);
    Task<List<string>> GetSessionExcerptAsync(OperationLogEntry entry, int maxBytes);
}

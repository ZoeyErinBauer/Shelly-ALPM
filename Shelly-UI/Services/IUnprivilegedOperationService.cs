using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shelly_UI.Services;

public interface IUnprivilegedOperationService
{
    Task<UnprivilegedOperationResult> RemoveFlatpakPackage(IEnumerable<string> packages);
    Task<UnprivilegedOperationResult> ListFlatpakPackages();
    
    Task<UnprivilegedOperationResult> ListFlatpakUpdates();
    
    Task<UnprivilegedOperationResult> ListAppstreamFlatpak();
}

public class UnprivilegedOperationResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}
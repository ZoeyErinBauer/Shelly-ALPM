using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmHookEvent
{
    public AlpmEventType Type; // 4 bytes
    public int When; // 4 bytes -  alpm_hook_when_t enum
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmOptDevRemovalEvent
{
    public AlpmEventType Type; // 4 bytes (offset 0)

    // 4 bytes padding (offset 4)
    public IntPtr Pkg; // 8 bytes (offset 8)
    public IntPtr Optdep; // 8 bytes (offset 16)
}
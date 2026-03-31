using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmPacsaveCreatedEvent
{
    public AlpmEventType Type; // 4 bytes

    // 4 bytes 
    public IntPtr OldPkg; // 8 Bytes
    public IntPtr File; // 8 Bytes
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmPacnewCreatedEvent
{
    public AlpmEventType Type; // 4 bytes
    public int FromNoUpgrade; // 4 bytes
    public IntPtr OldPkg; // 8 bytes
    public IntPtr NewPkg; // 8 bytes
    public IntPtr File; // 8 bytes
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmHookRunEvent
{
    public AlpmEventType Type; // 4 Bytes

    // 4 Bytes padding
    public IntPtr Name; // 8 Bytes
    public IntPtr Desc; // 8 Bytes
    public UIntPtr Position; // 8 Bytes
    public UIntPtr Total; //8 Bytes
}
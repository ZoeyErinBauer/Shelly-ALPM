using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Events;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmPkgRetrieveEvent
{
    public AlpmEventType Type; //4 Bytes

    // 4  bytes padding
    public UIntPtr Num; // 8 bytes (offset 8) - size_t
    public long TotalSize; //8 bytes (offset 16) - off_t
}
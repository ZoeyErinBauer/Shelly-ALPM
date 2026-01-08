using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

[StructLayout(LayoutKind.Sequential)]
internal struct AlpmList
{
    public IntPtr Data;
    public IntPtr Prev;
    public IntPtr Next;
}
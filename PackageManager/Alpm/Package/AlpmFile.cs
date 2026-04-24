using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Package;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmFile
{
    // const char*
    public IntPtr Name;

    // off_t
    public long Size;

    //mode_t
    public uint Mode;
}
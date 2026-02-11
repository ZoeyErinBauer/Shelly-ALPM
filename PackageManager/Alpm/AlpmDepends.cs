using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmDepends
{
    public IntPtr Name;
    public IntPtr Version;
    public IntPtr Desc;
    public ulong NameHash;
    public int Mod;
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct Conflict
{
    public ulong PackageOneHash;
    public ulong PackageTwoHash;
    public IntPtr PackageOne;
    public IntPtr PackageTwo;
    public IntPtr Reason;
}
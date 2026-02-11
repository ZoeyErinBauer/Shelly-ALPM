using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct ReplacePackage
{
    public int Type;
    public int Answer;
    public IntPtr OldPkg;
    public IntPtr NewPkg;
    public IntPtr NewDb;
}
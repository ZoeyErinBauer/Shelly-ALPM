using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct InstallIgnorePackage
{
    public int Type;
    public int Answer;
    public IntPtr Pkg; //Pointer reference to the offending package
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct CorruptedPackage
{
    public int Type;
    public int Answer;
    public IntPtr Filepath; //File location of the package
    public int Reason; // Represents  the alpm error number
}
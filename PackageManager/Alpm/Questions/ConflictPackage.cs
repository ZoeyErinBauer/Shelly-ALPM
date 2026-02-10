using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct ConflictPackage
{
    public int Type;
    public int Answer;
    public IntPtr Conflict; // Conflicting Package
}
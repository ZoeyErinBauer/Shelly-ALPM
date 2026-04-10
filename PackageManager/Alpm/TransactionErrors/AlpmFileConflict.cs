using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.TransactionErrors;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmFileConflict
{
    public IntPtr Target;
    public AlpmFileConflictType Type;
    public IntPtr File;

    //The other package that owns the file if ConflictType is Target.
    //Otherwise, the field is empty
    public IntPtr CTarget;

    public string? TargetName => Marshal.PtrToStringUTF8(Target);
    public string? FilePath => Marshal.PtrToStringUTF8(File);
    public string? CTargetName => Marshal.PtrToStringUTF8(CTarget);

    public override string ToString() => Type == AlpmFileConflictType.Target
        ? $"{TargetName} and {CTargetName} both contain {FilePath}"
        : $"{TargetName}: {FilePath} exists in filesystem.";
}
using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.TransactionErrors;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmDependencyMissing
{
    public IntPtr Target;
    public IntPtr Depend;
    public IntPtr CausingPkg;

    public string? TargetName => Marshal.PtrToStringUTF8(Target);
    public string? CausingPkgName => Marshal.PtrToStringUTF8(CausingPkg);

    public AlpmDepends GetDepend() => Depend == IntPtr.Zero ? default : Marshal.PtrToStructure<AlpmDepends>(Depend);

    public override string ToString()
    {
        var dep = GetDepend();
        var depName = Marshal.PtrToStringUTF8(dep.Name);
        var depVersion = Marshal.PtrToStringUTF8(dep.Version);
        var depStr = string.IsNullOrEmpty(depVersion) ? depName : $"{depName} {depVersion}";
        return CausingPkg != IntPtr.Zero
            ? $"Installing {CausingPkgName} breaks dependency on {depStr} required by {TargetName}"
            : $"Cannot resolve \"{depStr}\", a dependency of \"{TargetName}\"";
    }
}
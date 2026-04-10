using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.TransactionErrors;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmConflict
{
    public IntPtr PackageOne;
    public IntPtr PackageTwo;
    public IntPtr Reason;

    public string? PackageOneName => PackageOne != IntPtr.Zero ? new AlpmPackage(PackageOne).Name : null;
    public string? PackageTwoName => PackageTwo != IntPtr.Zero ? new AlpmPackage(PackageTwo).Name : null;

    public AlpmDepends GetReason() => Reason == IntPtr.Zero ? default : Marshal.PtrToStructure<AlpmDepends>(Reason);

    public override string ToString()
    {
        var reason = GetReason();
        var reasonName = Marshal.PtrToStringUTF8(reason.Name);
        return $"{PackageOneName} and {PackageTwoName} are in conflict ({reasonName})";
    }
}
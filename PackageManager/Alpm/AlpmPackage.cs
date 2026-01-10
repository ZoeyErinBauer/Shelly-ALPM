using System;

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

public class AlpmPackage(IntPtr pkgPtr)
{
    public IntPtr PackagePtr { get; } = pkgPtr;

    public string Name => Marshal.PtrToStringUTF8(AlpmReference.GetPkgName(PackagePtr));
    public string Version => Marshal.PtrToStringUTF8(AlpmReference.GetPkgVersion(PackagePtr));
    public long Size => AlpmReference.GetPkgSize(PackagePtr);

    public static List<AlpmPackage> FromList(IntPtr listPtr)
    {
        var packages = new List<AlpmPackage>();
        var currentPtr = listPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                packages.Add(new AlpmPackage(node.Data));
            }
            currentPtr = node.Next;
        }
        return packages;
    }
    
    public AlpmPackageDto ToDto() => new AlpmPackageDto
    {
        Name = Name,
        Version = Version,
        Size = Size
    };

    public override string ToString()
    {
        return $"Package: {Name}, Version: {Version}, Size: {Size} bytes";
    }
}
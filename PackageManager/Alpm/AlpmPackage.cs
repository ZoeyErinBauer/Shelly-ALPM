using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PackageManager.Alpm;

public class AlpmPackage(IntPtr pkgPtr)
{
    public IntPtr PackagePtr { get; } = pkgPtr != IntPtr.Zero
        ? pkgPtr
        : throw new ArgumentException("Package pointer cannot be null", nameof(pkgPtr));

    public string Name
    {
        get
        {
            var namePtr = AlpmReference.GetPkgName(PackagePtr);
            return namePtr != IntPtr.Zero 
                ? Marshal.PtrToStringUTF8(namePtr) ?? "unknown" 
                : "unknown";
        }
    }
    public string Version
    {
        get
        {
            var versionPtr = AlpmReference.GetPkgVersion(PackagePtr);
            return versionPtr != IntPtr.Zero 
                ? Marshal.PtrToStringUTF8(versionPtr) ?? "unknown" 
                : "unknown";
        }
    }
    
    public long Size => AlpmReference.GetPkgSize(PackagePtr);
    public string Description => Marshal.PtrToStringUTF8(AlpmReference.GetPkgDesc(PackagePtr))!;

    public string Url => Marshal.PtrToStringUTF8(AlpmReference.GetPkgUrl(PackagePtr))!;

    public List<string> Replaces => GetDependencyList(AlpmReference.GetPkgReplaces(PackagePtr));

    public string Repository
    {
        get
        {
            IntPtr dbPtr = AlpmReference.GetPkgDb(PackagePtr);
            if (dbPtr == IntPtr.Zero)
            {
                return "local"; // Or handle as an installed/local package
            }

            IntPtr namePtr = AlpmReference.DbGetName(dbPtr);
            return Marshal.PtrToStringUTF8(namePtr) ?? string.Empty;
        }
    }

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
        Size = Size,
        Description = Description,
        Url = Url,
        Repository = Repository,
        Replaces = Replaces,
    };

    public override string ToString()
    {
        return $"Package: {Name}, Version: {Version}, Size: {Size} bytes";
    }

    private static List<string> GetDependencyList(IntPtr listPtr)
    {
        var dependencies = new List<string>();
        var currentPtr = listPtr;
        while (currentPtr != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(currentPtr);
            if (node.Data != IntPtr.Zero)
            {
                var depString = AlpmReference.DepComputeString(node.Data);
                if (depString != IntPtr.Zero)
                {
                    var str = Marshal.PtrToStringUTF8(depString);
                    if (!string.IsNullOrEmpty(str))
                    {
                        dependencies.Add(str);
                    }
                }
            }

            currentPtr = node.Next;
        }

        return dependencies;
    }
}
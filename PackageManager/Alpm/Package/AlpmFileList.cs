using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Package;

[StructLayout(LayoutKind.Sequential)]
public struct AlpmFileList
{
    // size_t
    public nuint Count;

    // alpm_file_t *
    public IntPtr Files;
}
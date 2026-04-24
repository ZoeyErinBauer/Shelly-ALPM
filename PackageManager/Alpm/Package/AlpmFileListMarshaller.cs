using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Package;

public static class AlpmFileListMarshaller
{
    public static IEnumerable<AlpmFile> Enumerate(IntPtr fileListPtr)
    {
        if (fileListPtr == IntPtr.Zero) yield break;

        var list = Marshal.PtrToStructure<AlpmFileList>(fileListPtr);
        if (list.Files == IntPtr.Zero || list.Count == 0) yield break;

        var size = Marshal.SizeOf<AlpmFile>();
        for (nuint i = 0; i < list.Count; i++)
        {
            var elemPtr = IntPtr.Add(list.Files, checked((int)(i * (nuint)size)));
            yield return Marshal.PtrToStructure<AlpmFile>(elemPtr);
        }
    }
}

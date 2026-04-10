using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.TransactionErrors;

public static class AlpmInvalid
{
    public static List<string> WalkDataTree(IntPtr dataPtr)
    {
        List<string> results = [];
        var current = dataPtr;
        while (current != IntPtr.Zero)
        {
            var node = Marshal.PtrToStructure<AlpmList>(current);
            if (node.Data != IntPtr.Zero)
            {
                var pkgName = Marshal.PtrToStringUTF8(node.Data);
                results.Add(pkgName ?? "Unknown");
            }

            current = node.Next;
        }

        return results;
    }
}
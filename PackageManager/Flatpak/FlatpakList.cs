using System;
using System.Runtime.InteropServices;

namespace PackageManager.Flatpak;

[StructLayout(LayoutKind.Sequential)]
internal struct FlatpakList
{
    public IntPtr Data;
    public IntPtr Prev;
    public IntPtr Next;
}
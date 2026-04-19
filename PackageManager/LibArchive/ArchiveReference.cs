using System;
using System.Runtime.InteropServices;

namespace PackageManager.LibArchive;

internal static partial class ArchiveReference
{
    public const string LibName = "archive";
    
    [LibraryImport(LibName, EntryPoint = "archive_entry_pathname", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr ArchiveEntryPathname(IntPtr entry);

    [LibraryImport(LibName, EntryPoint = "archive_entry_size")]
    public static partial long ArchiveEntrySize(IntPtr entry);

    [LibraryImport(LibName, EntryPoint = "archive_entry_filetype")]
    public static partial int ArchiveEntryFileType(IntPtr entry);
    
    [LibraryImport(LibName, EntryPoint = "archive_entry_fflags_text")]
    public static partial IntPtr ArchiveEntryFflagsText(IntPtr entry);
    
    static ArchiveReference()
    {
        NativeResolver.Initialize();
    }
}
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("PackageManager.Tests")]

namespace PackageManager.Flatpak;

internal static partial class FlatpakReference
{
    public const string LibName = "flatpak";
    public const string GLibName = "glib-2.0";
    public const string GObjectName = "gobject-2.0";

    #region Installations

    [LibraryImport(LibName, EntryPoint = "flatpak_get_system_installations", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GetSystemInstallations(IntPtr cancellable, out IntPtr error);

    [LibraryImport(LibName, EntryPoint = "flatpak_installation_list_installed_refs", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstallationListInstalledRefs(IntPtr installation, IntPtr cancellable, out IntPtr error);

    #endregion

    #region Refs

    [LibraryImport(LibName, EntryPoint = "flatpak_remote_ref_get_remote_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr RemoteRefGetRemoteName(IntPtr @ref);
    
    [LibraryImport(LibName, EntryPoint = "flatpak_ref_get_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr RefGetName(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_ref_get_arch", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr RefGetArch(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_ref_get_branch", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr RefGetBranch(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_installed_ref_get_appdata_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstalledRefGetAppDataName(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_installed_ref_get_appdata_summary", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstalledRefGetAppDataSummary(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_installed_ref_get_appdata_version", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstalledRefGetAppDataVersion(IntPtr @ref);

    [LibraryImport(LibName, EntryPoint = "flatpak_installation_launch", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InstallationLaunch(IntPtr installation, string name, string? arch, string? branch, string? commit, IntPtr cancellable, out IntPtr error);

    [LibraryImport(LibName, EntryPoint = "flatpak_instance_get_child_pid", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int InstanceGetChildPid(IntPtr instance);

    [LibraryImport(LibName, EntryPoint = "flatpak_instance_get_all", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InstanceIsActive(IntPtr instance);

    [LibraryImport(LibName, EntryPoint = "flatpak_instance_get_all", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstanceGetAll();

    [LibraryImport(LibName, EntryPoint = "flatpak_instance_get_app", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstanceGetApp(IntPtr instance);

    #endregion

    #region GLib/GObject

    [LibraryImport(GObjectName, EntryPoint = "g_object_unref")]
    public static partial void GObjectUnref(IntPtr @object);

    [LibraryImport(GLibName, EntryPoint = "g_ptr_array_unref")]
    public static partial void GPtrArrayUnref(IntPtr array);

    #endregion

    #region Remotes

    [LibraryImport(LibName, EntryPoint = "flatpak_installation_list_remotes", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr InstallationListRemotes(IntPtr installation, IntPtr cancellable, out IntPtr error);

    [LibraryImport(LibName, EntryPoint = "flatpak_remote_get_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr RemoteGetName(IntPtr remote);

    #endregion

    #region Transaction

    [LibraryImport(LibName, EntryPoint = "flatpak_transaction_new_for_installation", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr TransactionNewForInstallation(IntPtr installation, IntPtr cancellable, out IntPtr error);

    [LibraryImport(LibName, EntryPoint = "flatpak_transaction_add_install", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TransactionAddInstall(IntPtr transaction, string remote, string @ref, IntPtr subpaths, out IntPtr error);
    
    [LibraryImport(LibName, EntryPoint = "flatpak_transaction_add_uninstall", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TransactionAddUninstall(IntPtr transaction, string @ref, out IntPtr error);

    [LibraryImport(LibName, EntryPoint = "flatpak_transaction_add_update", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TransactionAddUpdate(IntPtr transaction, string @ref, IntPtr subpaths, string? commit, out IntPtr error);

    
    [LibraryImport(LibName, EntryPoint = "flatpak_transaction_run", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TransactionRun(IntPtr transaction, IntPtr cancellable, out IntPtr error);

    #endregion
    
    // This static constructor sets up the resolver
    static FlatpakReference()
    {
        NativeLibrary.SetDllImportResolver(typeof(FlatpakReference).Assembly, ResolveFlatpak);
    }

    internal static IntPtr ResolveFlatpak(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == LibName)
        {
            string[] versions = { "libflatpak.so.0", "libflatpak.so" };
            foreach (var version in versions)
            {
                if (NativeLibrary.TryLoad(version, assembly, searchPath, out IntPtr handle)) return handle;
            }
        }
        else if (libraryName == GLibName)
        {
            string[] versions = { "libglib-2.0.so.0", "libglib-2.0.so" };
            foreach (var version in versions)
            {
                if (NativeLibrary.TryLoad(version, assembly, searchPath, out IntPtr handle)) return handle;
            }
        }
        else if (libraryName == GObjectName)
        {
            string[] versions = { "libgobject-2.0.so.0", "libgobject-2.0.so" };
            foreach (var version in versions)
            {
                if (NativeLibrary.TryLoad(version, assembly, searchPath, out IntPtr handle)) return handle;
            }
        }

        return IntPtr.Zero;
    }
}
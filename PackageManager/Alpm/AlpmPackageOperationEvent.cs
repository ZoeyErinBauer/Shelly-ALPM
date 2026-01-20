using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

// Base event - just the type field
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventAny
{
    public AlpmEventType Type;
}

// For ScriptletInfo event
// Native: { alpm_event_type_t type; const char *line; }
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventScriptletInfo
{
    public AlpmEventType Type;  // 4 bytes
    public IntPtr Line;         // 8 bytes on 64-bit (after 4 bytes padding)
}

// For PackageOperation events (Start/Done)
// Native: { alpm_event_type_t type; alpm_package_operation_t operation; alpm_pkg_t *oldpkg; alpm_pkg_t *newpkg; }
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmPackageOperationEvent
{
    public AlpmEventType Type;      // 4 bytes (alpm_event_type_t enum)
    public int Operation;           // 4 bytes (alpm_package_operation_t enum)
    public IntPtr OldPkgPtr;        // 8 bytes on 64-bit
    public IntPtr NewPkgPtr;        // 8 bytes on 64-bit
}

// For HookRun events
// Native: { alpm_event_type_t type; const char *name; const char *desc; size_t position; size_t total; }
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventHookRun
{
    public AlpmEventType Type;      // 4 bytes
    public IntPtr Name;             // 8 bytes on 64-bit (after 4 bytes padding)
    public IntPtr Desc;             // 8 bytes on 64-bit
    public UIntPtr Position;        // size_t - 8 bytes on 64-bit
    public UIntPtr Total;           // size_t - 8 bytes on 64-bit
}

// For DatabaseMissing events (not DatabaseSync - that doesn't exist!)
// Native: { alpm_event_type_t type; const char *dbname; }
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventDatabaseMissing
{
    public AlpmEventType Type;      // 4 bytes
    public IntPtr DbName;           // 8 bytes on 64-bit (after 4 bytes padding)
}

using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

/// <summary>
/// Structure for the SelectProvider question type in libalpm.
/// This is used when multiple packages can satisfy a dependency.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AlpmQuestionSelectProvider
{
    /// <summary>
    /// The question type (should be AlpmQuestionType.SelectProvider = 32)
    /// </summary>
    public int Type;

    /// <summary>
    /// Output: the index of the selected provider (0-based)
    /// </summary>
    public int UseIndex;

    /// <summary>
    /// Pointer to alpm_list_t* of alpm_pkg_t* - the list of provider packages
    /// </summary>
    public IntPtr Providers;

    /// <summary>
    /// Pointer to alpm_depend_t* - the dependency being resolved
    /// </summary>
    public IntPtr Depend;
}

using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm.Questions;

[StructLayout(LayoutKind.Sequential)]
public struct RemovePackages
{
    public int Type; // Should be AlpmQuestionType.RemovePkgs (64)
    public int Answer; // The user's response (usually 1 for yes, 0 for no)
    public IntPtr Pkgs; // A pointer to an alpm_list_t of packages to be removed
}
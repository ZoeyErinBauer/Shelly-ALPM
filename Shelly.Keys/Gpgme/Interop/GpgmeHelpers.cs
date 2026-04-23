using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Shelly.Keys.Gpgme.Interop;

internal static class GpgmeHelpers
{
    public static string? PtrToStringUTF8(IntPtr ptr)
    {
        return Marshal.PtrToStringUTF8(ptr);
    }
    
    public static void ThrowIfError(uint errorCode)
    {
        if (errorCode != (uint)GpgmeNative.gpg_err_code_t.GPG_ERR_NO_ERROR)
        {
            throw new Exceptions.GpgmeException(errorCode);
        }
    }
}

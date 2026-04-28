using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PackageManager.Utilities;

public static class XdgPaths
{
    public static string ConfigHome() => Resolve("XDG_CONFIG_HOME", ".config");
    public static string CacheHome() => Resolve("XDG_CACHE_HOME", ".cache");
    public static string DataHome() => Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));
    public static string StateHome() => Resolve("XDG_STATE_HOME", Path.Combine(".local", "state"));

    public static string ShellyCache(params string[] parts) =>
        Path.Combine([CacheHome(), "Shelly", .. parts]);

    public static string ShellyData(params string[] parts) =>
        Path.Combine([DataHome(), "Shelly", .. parts]);

    public static string ShellyConfig(params string[] parts) =>
        Path.Combine([ConfigHome(), "shelly", .. parts]);

    public static string InvokingUserHome()
    {
        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        if (!string.IsNullOrEmpty(sudoUser) && sudoUser != "root")
        {
            var home = GetHomeFromPasswd(sudoUser);
            if (!string.IsNullOrEmpty(home))
            {
                return home;
            }
        }

        var envHome = Environment.GetEnvironmentVariable("HOME");
        return !string.IsNullOrEmpty(envHome)
            ? envHome
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static bool HasSudoUser()
    {
        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        return !string.IsNullOrEmpty(sudoUser) && sudoUser != "root";
    }

    private static string Resolve(string envVar, string fallbackRel)
    {
        if (HasSudoUser()) return Path.Combine(InvokingUserHome(), fallbackRel);
        var v = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(v) && Path.IsPathRooted(v))
            return v;

        return Path.Combine(InvokingUserHome(), fallbackRel);
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Passwd
    {
        public byte* pw_name;
        public byte* pw_passwd;
        public uint pw_uid;
        public uint pw_gid;
        public byte* pw_gecos;
        public byte* pw_dir;
        public byte* pw_shell;
    }

    [DllImport("libc", EntryPoint = "getpwnam_r", SetLastError = true)]
    private static extern unsafe int getpwnam_r(
        byte* name,
        Passwd* pwd,
        byte* buf,
        nuint buflen,
        Passwd** result);

    private static unsafe string? GetHomeFromPasswd(string user)
    {
        try
        {
            var nameBytes = System.Text.Encoding.UTF8.GetByteCount(user) + 1;
            Span<byte> nameBuf = stackalloc byte[nameBytes];
            System.Text.Encoding.UTF8.GetBytes(user, nameBuf);
            nameBuf[nameBytes - 1] = 0;

            const int bufSize = 4096;
            var buf = Marshal.AllocHGlobal(bufSize);
            try
            {
                Passwd pwd;
                Passwd* result;
                fixed (byte* namePtr = nameBuf)
                {
                    var rc = getpwnam_r(namePtr, &pwd, (byte*)buf, bufSize, &result);
                    if (rc != 0 || result == null)
                    {
                        return GetHomeFromPasswdFile(user);
                    }

                    return pwd.pw_dir == null ? null : Marshal.PtrToStringUTF8((IntPtr)pwd.pw_dir);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        catch
        {
            return GetHomeFromPasswdFile(user);
        }
    }

    private static string? GetHomeFromPasswdFile(string user)
    {
        try
        {
            foreach (var line in File.ReadLines("/etc/passwd"))
            {
                var parts = line.Split(':');
                if (parts.Length >= 6 && parts[0] == user)
                    return parts[5];
            }
        }
        catch
        {
            Console.WriteLine("Failed to read /etc/passwd");
        }

        return null;
    }
}
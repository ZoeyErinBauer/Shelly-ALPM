using System.Diagnostics;

namespace Shelly_CLI;

public static class RootElevator
{
    public static void EnsureRootExectuion()
    {
        var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        if (user.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var args = Environment.GetCommandLineArgs();
        var exe = Environment.ProcessPath ?? args[0];
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                ArgumentList = { exe },
                UseShellExecute = false,

            }
        };
        foreach (var arg in args.Skip(1))
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        process.WaitForExit();
        Environment.Exit(process.ExitCode);
    }
}
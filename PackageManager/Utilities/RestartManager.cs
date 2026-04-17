using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PackageManager.Utilities;

public static class RestartManager
{
    public static (bool needsReboot, List<string> servicesNeedingRestart) CheckForRequiredRestarts()
    {
        HashSet<string> servicesNeedingRestart = [];
        var runningKernel = File.ReadAllText("/proc/sys/kernel/osrelease").Trim();
        var needsReboot = !Directory.Exists($"/usr/lib/modules/{runningKernel}");

        foreach (var process in Directory.GetDirectories("/proc"))
        {
            var pid = Path.GetFileName(process);
            if (!int.TryParse(pid, out _))
            {
                continue;
            }

            var mapsFile = Path.Combine(process, "maps");
            if (!File.Exists(mapsFile))
            {
                continue;
            }

            try
            {
                var maps = File.ReadAllText(mapsFile);
                if (!maps.Contains("(deleted)") || !maps.Contains(".so"))
                {
                    continue;
                }

                var cgroupFile = Path.Combine(process, "cgroup");
                if (!File.Exists(cgroupFile))
                {
                    continue;
                }

                var cgroup = File.ReadAllText(cgroupFile);
                var match = Regex.Match(
                    cgroup, @"/system\.slice/(.+\.service)");
                if (match.Success)
                {
                    servicesNeedingRestart.Add(match.Groups[1].Value);
                }

                var commFile = Path.Combine(process, "comm");
                if (File.Exists(commFile))
                {
                    var comm = File.ReadAllText(commFile).Trim();
                    if (comm is "systemd" or "dbus-daemon" or "dbus-broker")
                        needsReboot = true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
        }
        // Use  servicesNeedingRestart.ToList() after fixing above problems
        return (needsReboot,[]);
    }

    public static async Task<List<(string service, string error)>> RestartServicesAsync(List<string> services)
    {
        var failures = new List<(string service, string error)>();
        foreach (var svc in services)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"restart {svc}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                failures.Add((svc, stderr.Trim()));
        }
        return failures;
    }
}
using System.Linq;
using System.Text.Json;
using PackageManager.Alpm;

namespace Shelly.Worker;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No command provided.");
            return;
        }

        var command = args[0];
        using var manager = new AlpmManager();

        try
        {
            switch (command)
            {
                case "GetAvailablePackages":
                    manager.Initialize();
                    var available = manager.GetAvailablePackages();
                    Console.WriteLine(JsonSerializer.Serialize(available));
                    break;

                case "GetInstalledPackages":
                    manager.Initialize();
                    var installed = manager.GetInstalledPackages();
                    Console.WriteLine(JsonSerializer.Serialize(installed));
                    break;

                case "GetPackagesNeedingUpdate":
                    manager.IntializeWithSync();
                    var updates = manager.GetPackagesNeedingUpdate();
                    Console.WriteLine(JsonSerializer.Serialize(updates));
                    break;

                case "Sync":
                    manager.IntializeWithSync();
                    Console.WriteLine("Success");
                    break;

                case "InstallPackages":
                    if (args.Length < 2) throw new Exception("Missing packages list");
                    var packagesToInstall = JsonSerializer.Deserialize<List<string>>(args[1]);
                    manager.Initialize();
                    manager.InstallPackages(packagesToInstall!);
                    Console.WriteLine("Success");
                    break;

                case "UpdatePackages":
                    if (args.Length < 2) throw new Exception("Missing packages list");
                    var packagesToUpdate = JsonSerializer.Deserialize<List<string>>(args[1]);
                    manager.Initialize();
                    manager.UpdatePackages(packagesToUpdate!);
                    Console.WriteLine("Success");
                    break;

                case "RemovePackage":
                    if (args.Length < 2) throw new Exception("Missing package name");
                    manager.Initialize();
                    manager.RemovePackage(args[1]);
                    Console.WriteLine("Success");
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.Exit(1);
        }
    }
}

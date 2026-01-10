using System.Text.Json;
using PackageManager.Alpm;

namespace Shelly.Worker;

class Program
{
    static void Main(string[] args)
    {
        using var manager = new AlpmManager();
        manager.Initialize();

        while (true)
        {
            var line = Console.ReadLine();
            if (line == null) break;

            WorkerRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<WorkerRequest>(line);
            }
            catch
            {
                continue;
            }

            if (request == null) continue;

            var response = new WorkerResponse { Success = true };

            try
            {
                switch (request.Command)
                {
                    case "GetAvailablePackages":
                        //manager.Initialize();
                        var available = manager.GetAvailablePackages();
                        response.Data = JsonSerializer.Serialize(available);
                        break;

                    case "GetInstalledPackages":
                        //manager.Initialize();
                        var installed = manager.GetInstalledPackages();
                        response.Data = JsonSerializer.Serialize(installed);
                        break;

                    case "GetPackagesNeedingUpdate":
                        manager.Sync();
                        var updates = manager.GetPackagesNeedingUpdate();
                        response.Data = JsonSerializer.Serialize(updates);
                        break;

                    case "Sync":
                        manager.Sync();
                        break;

                    case "InstallPackages":
                        if (request.Payload == null) throw new Exception("Missing packages list");
                        var packagesToInstall = JsonSerializer.Deserialize<List<string>>(request.Payload);
                        manager.InstallPackages(packagesToInstall!);
                        break;

                    case "UpdatePackages":
                        if (request.Payload == null) throw new Exception("Missing packages list");
                        var packagesToUpdate = JsonSerializer.Deserialize<List<string>>(request.Payload);
                        manager.UpdatePackages(packagesToUpdate!);
                        break;

                    case "RemovePackage":
                        if (request.Payload == null) throw new Exception("Missing package name");
                        manager.RemovePackage(request.Payload);
                        break;
                    case "RemovePackages":
                        if (request.Payload == null) throw new Exception("Missing packages list");
                        var packagesToRemove = JsonSerializer.Deserialize<List<string>>(request.Payload);
                        manager.RemovePackages(packagesToRemove!);
                        break;
                    case "Exit":
                        return;

                    default:
                        response.Success = false;
                        response.Error = $"Unknown command: {request.Command}";
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.Message;
            }

            Console.WriteLine(JsonSerializer.Serialize(response));
        }
    }
}
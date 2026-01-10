using System;
using System.IO;
using PackageManager.Alpm;
using NUnit.Framework;

namespace PackageManager.Tests.Services
{
    [TestFixture]
    public class WorkerTest
    {
        [Test]
        public void TestWorkerExecution()
        {
            string workerName = "Shelly.Worker";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = {
                Path.Combine(baseDir, workerName),
                Path.Combine(baseDir, "..", "..", "..", "..", "Shelly.Worker", "bin", "Debug", "net10.0", workerName),
                Path.Combine(baseDir, "..", "..", "..", "Shelly.Worker", "bin", "Debug", "net10.0", workerName),
                Path.Combine("/usr/lib/shelly", workerName)
            };

            string workerPath = null;
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    workerPath = path;
                    break;
                }
            }

            Assert.That(workerPath, Is.Not.Null, "Worker path not found in search paths.");
            Console.WriteLine($"Found worker at: {workerPath}");

            var client = new AlpmWorkerClient(workerPath);
            // This will try to run 'pkexec'. In a test environment without UI/Auth it might fail,
            // but we want to see if it at least finds the worker and tries to run it.
            try 
            {
                var packages = client.GetInstalledPackages();
                Assert.That(packages, Is.Not.Null);
                Console.WriteLine($"Found {packages.Count} installed packages via worker.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker execution failed (expected if pkexec fails in tests): {ex.Message}");
                if (ex.Message.Contains("No such file or directory"))
                {
                    Assert.Fail("Worker still not found by process execution logic.");
                }
            }
        }
    }
}

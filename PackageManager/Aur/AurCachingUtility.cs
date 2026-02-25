using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;

namespace PackageManager.Aur;

public static class AurCachingUtility
{
    public static void CacheAurPackages()
    {
        var handler = new SocketsHttpHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            ConnectTimeout = TimeSpan.FromMinutes(1),
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(1)
        };
        var client = new HttpClient(handler);
        var result = client.GetStreamAsync("https://aur.archlinux.org/packages.gz").GetAwaiter().GetResult();
        var gZipStream = new GZipStream(result, CompressionMode.Decompress);
        var tarReader = new TarReader(gZipStream);

        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shelly");
        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }

        while (tarReader.GetNextEntry(true) is { } entry)
        {
            if (entry.Name.EndsWith(".json"))
            {
                entry.ExtractToFile(Path.Combine(configPath, "aur-packages.json"), true);
            }
        }
    }
}
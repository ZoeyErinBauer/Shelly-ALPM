using System;
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
        var result = client.GetStreamAsync("https://aur.archlinux.org/packages-meta-v1.json.gz").GetAwaiter().GetResult();
        var gZipStream = new GZipStream(result, CompressionMode.Decompress);

        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shelly");
        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }

        var filePath = Path.Combine(configPath, "aur-packages.json");
        using var fileStream = File.Create(filePath);
        gZipStream.CopyTo(fileStream);
    }
}
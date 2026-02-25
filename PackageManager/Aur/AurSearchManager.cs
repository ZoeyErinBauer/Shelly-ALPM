using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

public class AurSearchManager : IAurSearchManager, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://aur.archlinux.org/rpc/v5/";
    private readonly string _cacheFilePath;

    public AurSearchManager(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "shelly");
        _cacheFilePath = Path.Combine(configPath, "aur-packages.json");
    }

    public async Task<AurResponse<AurPackageDto>> SearchAsync(string query,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                await using var fs = File.OpenRead(_cacheFilePath);
                var cachedPackages = await JsonSerializer.DeserializeAsync<List<AurPackageDto>>(fs,
                    AurJsonContext.Default.ListAurPackageDto, cancellationToken);

                if (cachedPackages != null)
                {
                    var results = cachedPackages
                        .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    (p.Description != null &&
                                     p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        .Take(100)
                        .ToList();

                    if (results.Count > 0)
                    {
                        return new AurResponse<AurPackageDto>
                        {
                            Type = "search",
                            ResultCount = results.Count,
                            Results = results
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to API if cache reading fails
            }
        }

        var url = $"{BaseUrl}search/{Uri.EscapeDataString(query)}";
        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
    }

    public async Task<AurResponse<AurPackageDto>> SuggestAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}suggest/{Uri.EscapeDataString(query)}";
        var names = await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.ListString, cancellationToken);
        
        if (names == null || names.Count == 0)
            return new AurResponse<AurPackageDto> { Type = "suggest", Results = [] };
        
        // Fetch full package info for the suggested names
        return await GetInfoAsync(names, cancellationToken);
    }

    public async Task<AurResponse<AurPackageDto>> SuggestByPackageBaseNamesAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}suggest-pkgbase/{Uri.EscapeDataString(query)}";
        var names = await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.ListString, cancellationToken);
        
        if (names == null || names.Count == 0)
            return new AurResponse<AurPackageDto> { Type = "suggest", Results = [] };
        
        // Fetch full package info for the suggested names
        return await GetInfoAsync(names, cancellationToken);
    }

    public async Task<AurResponse<AurPackageDto>> GetInfoAsync(IEnumerable<string> packageNames,
        CancellationToken cancellationToken = default)
    {
        var names = packageNames.ToList();
        if (names.Count == 0)
        {
            return new AurResponse<AurPackageDto> { Type = "info", Results = [] };
        }

        // AUR RPC supports multiple names via arg[] parameter.
        // To minimize requests, we send them all in one go.
        // Note: There might be a limit on URL length or number of arguments, but for typical usage this is fine.
        var queryParams = string.Join("&", names.Select(n => $"arg[]={Uri.EscapeDataString(n)}"));
        var url = $"{BaseUrl}info?{queryParams}";

        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
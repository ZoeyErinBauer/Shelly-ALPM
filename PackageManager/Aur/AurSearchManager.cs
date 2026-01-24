using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

public class AurSearchManager : IAurSearchManager, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://aur.archlinux.org/rpc/v5/";

    public AurSearchManager(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AurResponse<AurPackageDto>> SearchAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}search/{Uri.EscapeDataString(query)}";
        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
    }

    public async Task<AurResponse<AurPackageDto>> SuggestAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}suggest/{Uri.EscapeDataString(query)}";
        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
    }

    public async Task<AurResponse<AurPackageDto>> SuggestByPackageBaseNamesAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}suggest-pkgbase/{Uri.EscapeDataString(query)}";
        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
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
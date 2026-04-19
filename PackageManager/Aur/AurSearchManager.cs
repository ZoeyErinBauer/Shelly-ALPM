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
    private const string BaseUrl = "https://aur.archlinux.org/rpc/";

    public AurSearchManager(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AurResponse<AurPackageDto>> SearchAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?v=5&type=search&arg={Uri.EscapeDataString(query)}&by=name-desc";
        var response =
            await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto, cancellationToken);
        return response ?? new AurResponse<AurPackageDto> { Type = "error", Error = "Empty response" };
    }

    public async Task<List<string>> SuggestAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?v=5&type=suggest&arg={Uri.EscapeDataString(query)}";
        return await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.ListString, cancellationToken) ?? [];
    }

    public async Task<List<string>>SuggestByPackageBaseNamesAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?v=5&type=suggest-pkgbase&arg={Uri.EscapeDataString(query)}";
        return await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.ListString, cancellationToken) ?? [];
    }

    public async Task<AurResponse<AurPackageDto>> GetInfoAsync(IEnumerable<string> packageNames,
        CancellationToken cancellationToken = default)
    {
        var names = packageNames.ToList();
        if (names.Count == 0)
        {
            return new AurResponse<AurPackageDto> { Type = "info", Results = [] };
        }

        const int chunkSize = 100;
        var allResults = new List<AurPackageDto>();
        var resultType = "info";

        for (var i = 0; i < names.Count; i += chunkSize)
        {
            var chunk = names.Skip(i).Take(chunkSize).ToList();
            var queryParams = string.Join("&", chunk.Select(n => $"arg[]={Uri.EscapeDataString(n)}"));
            var url = $"{BaseUrl}?v=5&type=info&{queryParams}";

            var response =
                await _httpClient.GetFromJsonAsync(url, AurJsonContext.Default.AurResponseAurPackageDto,
                    cancellationToken);

            if (response == null) continue;
            
            if (response.Type == "error")
            {
                return response;
            }

            resultType = response.Type;
            if (response.Results != null)
            {
                allResults.AddRange(response.Results);
            }
        }

        return new AurResponse<AurPackageDto>
        {
            Type = resultType,
            ResultCount = allResults.Count,
            Results = allResults
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
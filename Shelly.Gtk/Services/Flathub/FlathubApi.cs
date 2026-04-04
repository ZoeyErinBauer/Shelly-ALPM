using System.Text.Json;
using Shelly.Gtk.UiModels.PackageManagerObjects;

namespace Shelly.Gtk.Services.FlatHub;

public class FlatHubApiService : IFlatHubApiService
{
    private const string BaseUrl = "https://flathub.org/api/v2";

    private readonly HttpClient _httpClient = new();

    public async Task GetStatsForAppAsync(string appId)
    {
        var result = await _httpClient.GetAsync($"{BaseUrl}/apps/{appId}");
    }

    public async Task<List<string>> GetCollectionTrendingAsync(int page = 1, int perPage = 20)
    {
        return (await GetAppIdFromResponse(await _httpClient.GetAsync($"{BaseUrl}/collection/trending?page={page}&per_page={perPage}")))!;
    }

    public async Task<List<string>> GetCollectionPopularAsync(int page = 1, int perPage = 20)
    {
        return (await GetAppIdFromResponse(await _httpClient.GetAsync($"{BaseUrl}/collection/popular?page={page}&per_page={perPage}")))!;
    }

    public async Task<List<string>> GetCollectionRecentlyUpdatedAsync(int page = 1, int perPage = 20)
    {
        return (await GetAppIdFromResponse(await _httpClient.GetAsync($"{BaseUrl}/collection/recently-updated?page={page}&per_page={perPage}")))!;
    }

    public async Task<List<string>> GetCollectionRecentlyAddedAsync(int page = 1, int perPage = 20)
    {
        return (await GetAppIdFromResponse(await _httpClient.GetAsync($"{BaseUrl}/collection/recently-added?page={page}&per_page={perPage}")))!;
    }

    private static async Task<List<string?>> GetAppIdFromResponse(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<FlathubSearchResponse>(json,
            ShellyGtkJsonContext.Default.FlathubSearchResponse);
        return (searchResponse?.Hits == null ? [] : searchResponse.Hits.Select(app => app.AppId).ToList())!;
    }
}
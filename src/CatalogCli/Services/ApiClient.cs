using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EstimatorMcp.Models;

namespace CatalogCli.Services;

public class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public ApiClient(string baseUrl, string token)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> GetTsvAsync(string path)
    {
        var response = await _http.GetAsync(path);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<CatalogData> GetCatalogAsync()
    {
        var response = await _http.GetAsync("api/catalog/export");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CatalogData>(JsonOptions)
               ?? throw new InvalidOperationException("Server returned empty catalog.");
    }

    public async Task ImportCatalogAsync(CatalogData catalog)
    {
        var response = await _http.PostAsJsonAsync("api/catalog/import", catalog, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }

    public void Dispose() => _http.Dispose();
}

using System.Net.Http.Json;

namespace CompanyCLI.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(
                "https://localhost:5001/"
            )
        };
    }

    public async Task<T?> GetAsync<T>(
        string endpoint)
    {
        return await _httpClient
            .GetFromJsonAsync<T>(endpoint);
    }
}
using System.Text;
using System.Text.Json;
using SocialApp.ClientApi.Services;
using SocialApp.Models.Account;

namespace SocialApp.ClientApi;

public class SocialAppClient
{
    private readonly Uri _baseAddress;
    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AccountService Account { get; private set; }

    public SocialAppClient(Uri baseAddress)
    {
        _baseAddress = baseAddress;
        _httpClient = new HttpClient()
        {
            BaseAddress = baseAddress
        };
        Account = new AccountService(this);
    }

    public async Task<T> PostAsync<T>(string path, RegisterRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync("/register", new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
    }
}
using System.Text;
using System.Text.Json;
using SocialApp.ClientApi.Services;

namespace SocialApp.ClientApi;

public sealed class SocialAppClient
{
    private readonly Uri _baseAddress;
    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AccountService Account { get; private set; }
    public SessionService Session { get; private set; }
    public FollowService Follow { get; private set; }

    public SocialAppClient(Uri baseAddress)
    {
        _baseAddress = baseAddress;
        _httpClient = new HttpClient()
        {
            BaseAddress = baseAddress
        };
        Account = new AccountService(this);
        Session = new SessionService(this);
        Follow = new FollowService(this);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
    }
    
    public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.GetAsync(path, cancel);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
    }
    
    public async Task PostAsync<TRequest>(string path, TRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task DeleteAsync(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.DeleteAsync(path, cancel);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task PostAsync(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, null, cancel);
        response.EnsureSuccessStatusCode();
    }
}
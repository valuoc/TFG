using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SocialApp.ClientApi.Services;

namespace SocialApp.ClientApi;

public class Response
{
    public required HttpResponseHeaders Headers { get; init; }
}

public sealed class Response<T> : Response
{
    public required T Content { get; init; }
}

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
    public ContentService Content { get; private set; }
    public FeedService Feed { get; private set; }

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
        Content = new ContentService(this);
        Feed = new FeedService(this);
    }

    internal async Task<Response<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
        var content = JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
        return new Response<TResponse>()
        {
            Headers = response.Headers,
            Content = content
        };
    }
    
    internal async Task<Response<TResponse>> GetAsync<TResponse>(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.GetAsync(path, cancel);
        response.EnsureSuccessStatusCode();
        var content = JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
        return new Response<TResponse>()
        {
            Headers = response.Headers,
            Content = content
        };
    }
    
    internal async Task<Response> PostAsync<TRequest>(string path, TRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
        return new Response()
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> DeleteAsync(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.DeleteAsync(path, cancel);
        response.EnsureSuccessStatusCode();
        return new Response()
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> PostAsync(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.PostAsync(path, null, cancel);
        response.EnsureSuccessStatusCode();
        return new Response()
        {
            Headers = response.Headers,
        };
    }

    internal async Task<Response> PutAsync<TRequest>(string path, TRequest request, CancellationToken cancel)
    {
        using var response = await _httpClient.PutAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        response.EnsureSuccessStatusCode();
        return new Response()
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> PutAsync(string path, CancellationToken cancel)
    {
        using var response = await _httpClient.PutAsync(path, null, cancel);
        response.EnsureSuccessStatusCode();
        return new Response()
        {
            Headers = response.Headers,
        };
    }
}
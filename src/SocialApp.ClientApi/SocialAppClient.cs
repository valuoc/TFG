using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SocialApp.ClientApi.Clients;

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

    private readonly JsonSerializerOptions _jOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AccountService Account { get; private set; }
    public SessionClient Session { get; private set; }
    public FollowClient Follow { get; private set; }
    public ContentClient Content { get; private set; }
    public FeedClient Feed { get; private set; }

    public SocialAppClient(Uri baseAddress)
    {
        _baseAddress = baseAddress;
        _httpClient = new HttpClient
        {
            BaseAddress = baseAddress
        };
        Account = new AccountService(this);
        Session = new SessionClient(this);
        Follow = new FollowClient(this);
        Content = new ContentClient(this);
        Feed = new FeedClient(this);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)
            return;
        
        var content = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}):\n\t{content}", null, response.StatusCode) ;
    }

    internal async Task<Response<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancel)
    {
        PrintRequest("POST", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        var content = JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
        return new Response<TResponse>
        {
            Headers = response.Headers,
            Content = content
        };
    }
    
    internal async Task<Response<TResponse>> GetAsync<TResponse>(string path, CancellationToken cancel)
    {
        PrintRequest("GET", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.GetAsync(path, cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        var content = JsonSerializer.Deserialize<TResponse>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
        return new Response<TResponse>
        {
            Headers = response.Headers,
            Content = content
        };
    }
    
    internal async Task<Response> PostAsync<TRequest>(string path, TRequest request, CancellationToken cancel)
    {
        PrintRequest("POST", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        return new Response
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> DeleteAsync(string path, CancellationToken cancel)
    {
        PrintRequest("DELETE", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.DeleteAsync(path, cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        return new Response
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> PostAsync(string path, CancellationToken cancel)
    {
        PrintRequest("POST", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsync(path, null, cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        return new Response
        {
            Headers = response.Headers,
        };
    }

    internal async Task<Response> PutAsync<TRequest>(string path, TRequest request, CancellationToken cancel)
    {
        PrintRequest("PUT", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PutAsync(path, new StringContent(JsonSerializer.Serialize(request, _jOptions), Encoding.UTF8, "application/json"), cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        return new Response
        {
            Headers = response.Headers,
        };
    }
    
    internal async Task<Response> PutAsync(string path, CancellationToken cancel)
    {
        PrintRequest("PUT", path);
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.PutAsync(path, null, cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        return new Response
        {
            Headers = response.Headers,
        };
    }

    public async Task<JsonObject> HealthAsync(CancellationToken cancel = default)
    {
        PrintRequest("GET", "/health");
        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.GetAsync("/health", cancel);
        PrintResponse(response, sw);
        await EnsureSuccessAsync(response);
        var content = JsonSerializer.Deserialize<JsonObject>(await response.Content.ReadAsStreamAsync(cancel), _jOptions);
        return content;
    }

    private void PrintResponse(HttpResponseMessage response, Stopwatch sw)
    {
        var cost = response.Headers.TryGetValues("X-OperationCharge", out var values)
            ? values.FirstOrDefault()
            : null;
        cost = cost != null ? $"{cost} RUs" : string.Empty;
        
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($" HTTP_{(int)response.StatusCode} {response.StatusCode}, {(int)sw.ElapsedMilliseconds} ms, {cost} \n");
        Console.ResetColor();
    }

    private void PrintRequest(string method, string path)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($" -> {method} {_httpClient.BaseAddress}{path[1..]} ... ");
        Console.ResetColor();
    }
}

